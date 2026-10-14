using System.Buffers.Binary;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class OpaqueStorageCoreTests
{
    [Test]
    public async Task RawRecordsAndKeys_ReopenThroughCallerOwnedMetadata()
    {
        var directory=Path.Combine(Path.GetTempPath(),"opaque-storage-core-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var path=Path.Combine(directory,"database.db");
        var payload=new byte[]{0,255,128,0,1,2,3,4,5,6};var key=IndexKeyExposure.CreateIndexKey(new byte[]{0,255,1});RowId rowId;
        try{
         await using(var database=await PageDatabaseExposure.CreatePageDatabaseExposureAsync(path)){
          await using(var buffers=new BufferPoolExposure(database,32,true)){
           var heap=await TableHeapExposure.CreateTableHeapExposureAsync(buffers,database);rowId=await heap.InsertAsync(payload);
           var roots=new IndexRootReferenceExposure(default);var tree=await PersistentBPlusTreeExposure.CreatePersistentBPlusTreeExposureAsync(buffers,database,roots,true);await tree.InsertAsync(key,rowId,new byte[]{7,255});
           if(!(await tree.FindAsync(key)).Contains(rowId))throw new Exception("Index lookup failed");await buffers.FlushAllAsync();
           var metadata=new byte[16];BinaryPrimitives.WriteUInt64LittleEndian(metadata,heap.ReadRootPageId().Value);BinaryPrimitives.WriteUInt64LittleEndian(metadata.AsSpan(8),roots.ReadRootPageId().Value);
           var chain=new CatalogPageChainExposure(database,database);var written=await chain.WriteAsync(metadata);await database.PublishCatalogRootAsync(written.RootPageId);await database.FlushAsync();
          }
         }
         await using(var database=await PageDatabaseExposure.OpenAsync(path)){
          if(database.ReadHeader().FormatVersion!=5)throw new Exception("Wrong physical format");var chain=new CatalogPageChainExposure(database,database);var metadata=await chain.ReadAsync(database.ReadHeader().CatalogRootPageId!.Value);
          await using(var buffers=new BufferPoolExposure(database,32,true)){
           var heap=await TableHeapExposure.OpenAsync(new PageId{Value=BinaryPrimitives.ReadUInt64LittleEndian(metadata)},buffers,database);var record=await heap.ReadAsync(rowId);if(!record.Row.Span.SequenceEqual(payload))throw new Exception("Opaque record changed on reopen");
           var tree=new PersistentBPlusTreeExposure(buffers,database,new IndexRootReferenceExposure(new PageId{Value=BinaryPrimitives.ReadUInt64LittleEndian(metadata.AsSpan(8))}),true);var entries=await tree.FindEntriesAsync(key);if(entries.Count!=1||entries[0].RowId!=rowId||!LeafIndexEntryExposure.ReadLeafIndexEntryPayload(entries[0]).Span.SequenceEqual(new byte[]{7,255}))throw new Exception("Opaque index changed on reopen");
          }
         }
        
        }finally{Directory.Delete(directory,true);}
    }
}
