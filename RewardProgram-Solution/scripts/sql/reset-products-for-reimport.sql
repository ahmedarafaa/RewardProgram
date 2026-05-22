-- ============================================================================
-- Reset products for a clean re-import.
--
-- WHY: an earlier code-first Excel import — run before the header-aware importer
-- was deployed — stored Name and ProductCode in each other's columns. The
-- Products table now also holds duplicate codes (e.g. DP-F203004 twice) and
-- test rows, so a blind in-place "un-swap" UPDATE would hit the unique
-- ProductCode index and fail. The clean fix is to wipe all products and
-- re-import the correct ERP file with the (deployed) header-aware importer.
--
-- SAFETY: aborts if any ScanRecords exist (barcodes were scanned → wiping
-- products/barcodes would corrupt wallet history). Wrapped in a transaction
-- that defaults to ROLLBACK.
--
-- HOW TO USE
--   1. Deploy the latest build (header-aware import) to this environment FIRST,
--      otherwise the re-import in step 4 will swap the columns again.
--   2. Run this script as-is — it prints before/after counts, then ROLLBACKs
--      so you can verify nothing unexpected is touched.
--   3. If the counts look right, change the final ROLLBACK TRAN to COMMIT TRAN
--      and run the script again to apply.
--   4. Re-import the correct product .xlsx via the admin dashboard
--      (Products -> Import from Excel).
--
-- DO NOT run against Production without a fresh database backup.
-- ============================================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRY
    BEGIN TRAN;

    -- Safety assertion: refuse to run if any scans exist — that would mean
    -- barcodes are in use and wiping them would corrupt wallet/redemption data.
    DECLARE @scans INT = (SELECT COUNT(*) FROM ScanRecords);
    IF @scans > 0
    BEGIN
        PRINT '=== ABORT: ScanRecords present (' + CAST(@scans AS VARCHAR) + ') ===';
        PRINT 'Barcodes have been scanned. Wiping products/barcodes would corrupt';
        PRINT 'wallet history. Investigate before proceeding.';
        ROLLBACK TRAN;
        RETURN;
    END;

    DECLARE @productsBefore INT = (SELECT COUNT(*) FROM Products);
    DECLARE @barcodesBefore INT = (SELECT COUNT(*) FROM ProductBarcodes);
    PRINT '=== BEFORE ===';
    PRINT 'Products:        ' + CAST(@productsBefore AS VARCHAR);
    PRINT 'ProductBarcodes: ' + CAST(@barcodesBefore AS VARCHAR);

    -- ProductBarcodes FK-reference Products, so clear them first.
    DELETE FROM ProductBarcodes;
    DELETE FROM Products;

    PRINT '=== AFTER ===';
    PRINT 'Products:        ' + CAST((SELECT COUNT(*) FROM Products) AS VARCHAR);
    PRINT 'ProductBarcodes: ' + CAST((SELECT COUNT(*) FROM ProductBarcodes) AS VARCHAR);
    PRINT 'Deleted products: ' + CAST(@productsBefore AS VARCHAR);

    -- Default safety: roll back. Change to COMMIT TRAN once verified.
    ROLLBACK TRAN;
    -- COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH;
