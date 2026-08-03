# Recovery

Startup validates WAL database identity and timeline before inspecting records. Analysis classifies transactions as committed, rolled back, or incomplete and records the first change LSN for each dirty page. Only a record envelope extending beyond final EOF is an incomplete tail; it is ignored and the WAL is truncated to the preceding checksum-valid boundary. Invalid framing, values, or checksums before that boundary are corruption and stop open.

Physical page-change records contain bounded full before- and after-images. Redo applies committed after-images only when their record LSN exceeds the stored page LSN, making replay idempotent. Page identity, type, image checksum, and image LSN are verified first. A corrupt current page may be repaired only from that verified full-page image; an invalid logged image stops recovery.

Undo processes incomplete transactions backward through their previous-LSN chains and restores verified before-images for heap, index, catalog, and overflow pages. Each completed action appends and flushes a compensation record linked to the next action requiring undo. A crash during undo therefore resumes behind already completed work. When the chain is exhausted, a rollback record is appended and flushed; committed transactions are never undone.
