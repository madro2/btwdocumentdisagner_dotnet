BEGIN;

ALTER TABLE "PdfDesignImages"
    ADD COLUMN IF NOT EXISTS "ResourceKey" text NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PdfDesignImages_ResourceKey"
    ON "PdfDesignImages" ("ResourceKey")
    WHERE "ResourceKey" IS NOT NULL;

COMMIT;
