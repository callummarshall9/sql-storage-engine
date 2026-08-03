# Overflow format version 1

Overflow chains exclusively own their pages. A row stores a 16-byte little-endian reference: eight-byte first `PageId`, followed by an eight-byte signed total length. Page zero is never valid, and total length is between 1 byte and 64 MiB.

Each overflow page begins with the 32-byte common page header and uses page type `Overflow`.

| Offset | Width | Field | Validation |
|---:|---:|---|---|
| 32 | 1 | Next-page presence | `0` or `1` |
| 33 | 8 | Next page ID | Zero iff absent; never page zero or self |
| 41 | 4 | Used payload length | `1..page size - 48` |
| 45 | 3 | Reserved | Zero |
| 48 | used length | Payload | Raw bytes |
| remainder | variable | Unused | Written as zero |

The common checksum covers the complete page. Readers are bounded to at most 8,192 pages and 64 MiB, validate type, identity, checksum, links, used lengths, cycles, and the exact reference length before returning bytes.
