using sql_storage_engine.Identifiers;

namespace sql_storage_engine.UnitTests;

public class IdentifierTests
{
    [Test]
    public void RowIdEqualityWorksAsExpected()
    {
        //given
        var firstPageId = new PageId(1);
        var firstSlotId = new SlotId(1);
        var firstSlotGenerationId = new SlotGeneration(1);
        
        var secondPageId = new PageId(1);
        var secondSlotId = new SlotId(1);
        var secondSlotGenerationId = new SlotGeneration(1);

        //when
        var firstRow = new RowId(firstPageId, firstSlotId, firstSlotGenerationId);
        var secondRow = new RowId(secondPageId, secondSlotId, secondSlotGenerationId);

        //then
        Assert.That(firstRow == secondRow);
    }
    
    [Test]
    public void NewDatabaseIdEqualityWorksAsExpected()
    {
        //given
        var firstDatabaseId = DatabaseId.New();
        var secondDatabaseId = DatabaseId.New();
        
        //then
        Assert.That(firstDatabaseId != secondDatabaseId);
    }

    [Test]
    public void EqualDatabaseIdEqualityWorksAsExpected()
    {
        Guid sharedId = Guid.NewGuid();
        var firstDatabaseId = new DatabaseId(sharedId);
        var secondDatabaseId = new  DatabaseId(sharedId);
        
        Assert.That(firstDatabaseId == secondDatabaseId);
    }
}