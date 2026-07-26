using ERP.Application.LocalData;
using ERP.Application.MasterData.Customers;
using ERP.Application.Payments;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Application.Shortages;
using ERP.Application.TestData;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.Infrastructure.TestData;

public sealed class OrganizationTestDataService(
    AppDbContext dbContext,
    IHostEnvironment hostEnvironment,
    ILocalStoragePathService localStoragePathService,
    IOrganizationScopedToolExecutionContext toolExecutionContext,
    IPurchaseOrderService purchaseOrderService,
    IPurchaseOrderPostingService purchaseOrderPostingService,
    IPurchaseReceiptService purchaseReceiptService,
    IPurchaseReceiptPostingService purchaseReceiptPostingService,
    IShortageResolutionService shortageResolutionService,
    IShortageResolutionPostingService shortageResolutionPostingService,
    IPaymentService paymentService,
    ISupplierPaymentPostingService paymentPostingService,
    IPurchaseReturnService purchaseReturnService,
    IPurchaseReturnPostingService purchaseReturnPostingService,
    IDatabaseBackupService? databaseBackupService = null) : IOrganizationTestDataService
{
    private const int ManifestVersion = 1;
    private const string SupportedProfile = "restore-smoke";
    private const string Actor = "highcool-tool";
    private const string MarkerPrefix = "HC-RESTORE-SMOKE";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<OrganizationTestDataCommandResult> SeedAsync(
        SeedOrganizationTestDataRequest request,
        CancellationToken cancellationToken)
    {
        var guard = await ValidateSafeCommandAsync(request.OrganizationId, request.Profile, cancellationToken);
        if (guard is not null)
        {
            return guard with { DryRun = request.DryRun };
        }

        var scale = NormalizeScale(request.Scale);
        if (scale is null)
        {
            return Rejected(request.OrganizationId, request.Profile, BuildRunId(request.OrganizationId, request.Profile, request.Seed), request.DryRun, "Scale must be small, medium, or large.");
        }

        toolExecutionContext.SetOrganization(request.OrganizationId);
        localStoragePathService.EnsureRequiredDirectories();

        var marker = BuildMarker(request.OrganizationId, request.Seed);
        var runId = BuildRunId(request.OrganizationId, request.Profile, request.Seed);
        var manifestPath = GetManifestPath(runId);
        var snapshotPath = GetSnapshotPath(runId);
        var existingManifest = File.Exists(manifestPath);

        if (request.DryRun)
        {
            var planned = BuildPlannedSeedCounts(scale);
            return new OrganizationTestDataCommandResult(
                OrganizationTestDataCommandStatus.Planned,
                "Dry-run completed. No database rows were written.",
                request.OrganizationId,
                request.Profile,
                runId,
                true,
                manifestPath,
                snapshotPath,
                null,
                planned,
                existingManifest ? ["A manifest already exists for this deterministic seed run; use --force to replace it."] : []);
        }

        if (existingManifest && !request.Force)
        {
            return Rejected(request.OrganizationId, request.Profile, runId, false, "Seed run already exists. Re-run with --force to remove and recreate only this seed run.");
        }

        if (existingManifest && request.Force)
        {
            await DeleteManifestScopeAsync(await ReadManifestAsync(manifestPath, cancellationToken), cancellationToken);
        }

        var seeded = await SeedRestoreSmokeAsync(request, scale, marker, cancellationToken);
        var snapshot = await BuildSnapshotAsync(request.OrganizationId, request.Profile, runId, cancellationToken);
        var manifest = new OrganizationTestDataManifest(
            ManifestVersion,
            request.OrganizationId,
            request.Profile,
            scale.Name,
            request.Seed,
            runId,
            marker,
            DateTime.UtcNow,
            seeded.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Guid>)pair.Value),
            snapshot);

        await WriteJsonAsync(manifestPath, manifest, cancellationToken);
        await WriteJsonAsync(snapshotPath, snapshot, cancellationToken);

        return new OrganizationTestDataCommandResult(
            OrganizationTestDataCommandStatus.Completed,
            "Seed data created successfully.",
            request.OrganizationId,
            request.Profile,
            runId,
            false,
            manifestPath,
            snapshotPath,
            null,
            snapshot.Counts,
            []);
    }

    public async Task<OrganizationTestDataCommandResult> ResetAsync(
        ResetOrganizationDataRequest request,
        CancellationToken cancellationToken)
    {
        var profile = request.TestDataOnly ? SupportedProfile : "full-org-reset";
        var runId = request.SeedRunId ?? BuildRunId(request.OrganizationId, SupportedProfile, 1);
        var guard = await ValidateSafeCommandAsync(request.OrganizationId, SupportedProfile, cancellationToken);
        if (guard is not null)
        {
            return guard with { Profile = profile, RunId = runId, DryRun = request.DryRun };
        }

        if (request.Execute && !request.DryRun)
        {
            var expectedConfirmation = $"RESET-ORG-{request.OrganizationId}";
            if (!string.Equals(request.Confirmation, expectedConfirmation, StringComparison.Ordinal))
            {
                return Rejected(request.OrganizationId, profile, runId, false, $"Reset requires --confirmation {expectedConfirmation}.");
            }
        }

        toolExecutionContext.SetOrganization(request.OrganizationId);
        localStoragePathService.EnsureRequiredDirectories();

        OrganizationTestDataManifest? manifest = null;
        string? manifestPath = null;
        if (request.TestDataOnly)
        {
            manifestPath = GetManifestPath(runId);
            if (!File.Exists(manifestPath))
            {
                return Rejected(request.OrganizationId, profile, runId, request.DryRun, "Test-data-only reset requires a seed manifest for the selected run id.");
            }

            manifest = await ReadManifestAsync(manifestPath, cancellationToken);
            if (manifest.OrganizationId != request.OrganizationId)
            {
                return Rejected(request.OrganizationId, profile, runId, request.DryRun, "Manifest organization id does not match the reset organization id.");
            }
        }

        var plannedCounts = request.TestDataOnly && manifest is not null
            ? BuildManifestCounts(manifest)
            : await CountFullOrganizationScopeAsync(request.OrganizationId, cancellationToken);

        if (request.DryRun || !request.Execute)
        {
            return new OrganizationTestDataCommandResult(
                OrganizationTestDataCommandStatus.Planned,
                "Reset plan completed. No database rows were deleted.",
                request.OrganizationId,
                profile,
                runId,
                true,
                manifestPath,
                null,
                null,
                plannedCounts,
                BuildResetWarnings(request));
        }

        string? safetyBackupId = null;
        if (!request.SkipSafetyBackup || !request.TestDataOnly)
        {
            if (databaseBackupService is null)
            {
                return Rejected(request.OrganizationId, profile, runId, false, "Safety backup service is not available.");
            }

            var backup = await databaseBackupService.CreateBackupAsync(BackupReason.Manual, cancellationToken);
            if (backup.Status != BackupStatus.Succeeded)
            {
                return Rejected(request.OrganizationId, profile, runId, false, $"Safety backup failed: {backup.Message}");
            }

            safetyBackupId = backup.BackupId;
        }

        if (request.TestDataOnly && manifest is not null)
        {
            await DeleteManifestScopeAsync(manifest, cancellationToken);
            TryDelete(manifestPath);
            TryDelete(GetSnapshotPath(runId));
        }
        else
        {
            await DeleteFullOrganizationScopeAsync(request.OrganizationId, cancellationToken);
        }

        return new OrganizationTestDataCommandResult(
            OrganizationTestDataCommandStatus.Completed,
            "Reset completed successfully.",
            request.OrganizationId,
            profile,
            runId,
            false,
            manifestPath,
            null,
            safetyBackupId,
            plannedCounts,
            []);
    }

    public async Task<OrganizationTestDataCommandResult> VerifyAsync(
        VerifyOrganizationRestoreRequest request,
        CancellationToken cancellationToken)
    {
        var guard = await ValidateSafeCommandAsync(request.OrganizationId, SupportedProfile, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        if (!File.Exists(request.SnapshotPath))
        {
            return Rejected(request.OrganizationId, SupportedProfile, string.Empty, false, "Snapshot file was not found.");
        }

        toolExecutionContext.SetOrganization(request.OrganizationId);
        var expected = await ReadSnapshotAsync(request.SnapshotPath, cancellationToken);
        if (expected.OrganizationId != request.OrganizationId)
        {
            return Rejected(request.OrganizationId, expected.Profile, expected.RunId, false, "Snapshot organization id does not match the verify organization id.");
        }

        var actual = await BuildSnapshotAsync(request.OrganizationId, expected.Profile, expected.RunId, cancellationToken);
        var mismatches = expected.Counts
            .Where(pair => !actual.Counts.TryGetValue(pair.Key, out var actualValue) || actualValue != pair.Value)
            .Select(pair => $"{pair.Key}: expected {pair.Value}, actual {(actual.Counts.TryGetValue(pair.Key, out var value) ? value : 0)}")
            .ToArray();

        var totalMismatches = expected.Totals
            .Where(pair => !actual.Totals.TryGetValue(pair.Key, out var actualValue) || actualValue != pair.Value)
            .Select(pair => $"{pair.Key}: expected {pair.Value}, actual {(actual.Totals.TryGetValue(pair.Key, out var value) ? value : 0m)}")
            .ToArray();

        if (mismatches.Length > 0 || totalMismatches.Length > 0)
        {
            return new OrganizationTestDataCommandResult(
                OrganizationTestDataCommandStatus.Failed,
                "Restore verification failed.",
                request.OrganizationId,
                expected.Profile,
                expected.RunId,
                false,
                null,
                request.SnapshotPath,
                null,
                actual.Counts,
                mismatches.Concat(totalMismatches).ToArray());
        }

        return new OrganizationTestDataCommandResult(
            OrganizationTestDataCommandStatus.Completed,
            "Restore verification passed.",
            request.OrganizationId,
            expected.Profile,
            expected.RunId,
            false,
            null,
            request.SnapshotPath,
            null,
            actual.Counts,
            []);
    }

    private async Task<Dictionary<string, List<Guid>>> SeedRestoreSmokeAsync(
        SeedOrganizationTestDataRequest request,
        ScaleDefinition scale,
        string marker,
        CancellationToken cancellationToken)
    {
        var ids = NewIdMap();
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc).AddDays(request.Seed % 30);
        var token = BuildCodeToken(request.OrganizationId, request.Seed);

        var supplier = new Supplier
        {
            Code = $"{token}-SUP",
            Name = $"{marker} Supplier",
            StatementName = $"{marker} Supplier",
            Phone = "+2000000000",
            Email = "restore-smoke@highcool.test",
            City = "Cairo",
            Area = "Test",
            Notes = marker,
            IsActive = true,
            CreatedBy = Actor
        };
        var warehouse = new Warehouse
        {
            Code = $"{token}-WH",
            Name = $"{marker} Warehouse",
            Location = "Restore smoke shelf",
            IsActive = true,
            CreatedBy = Actor
        };
        var pcs = new Uom
        {
            Code = $"{token}-PCS",
            Name = "Restore smoke pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = Actor
        };
        var box = new Uom
        {
            Code = $"{token}-BOX",
            Name = "Restore smoke box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = Actor
        };
        var reason = new ShortageReasonCode
        {
            Code = $"{token}-SHORT",
            Name = "Restore smoke supplier shortage",
            Description = marker,
            AffectsSupplierBalance = true,
            AffectsStock = false,
            IsActive = true,
            CreatedBy = Actor
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        dbContext.Uoms.AddRange(pcs, box);
        dbContext.ShortageReasonCodes.Add(reason);

        for (var index = 1; index <= scale.CustomerCount; index++)
        {
            var customer = new Customer
            {
                Code = $"{token}-CUS-{index:00}",
                Name = $"{marker} Customer {index:00}",
                Phone = $"+201000000{index:000}",
                City = "Cairo",
                Area = "Test",
                Notes = marker,
                IsActive = true,
                CreatedBy = Actor
            };
            dbContext.Customers.Add(customer);
            ids[nameof(Customer)].Add(customer.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        ids[nameof(Supplier)].Add(supplier.Id);
        ids[nameof(Warehouse)].Add(warehouse.Id);
        ids[nameof(Uom)].Add(pcs.Id);
        ids[nameof(Uom)].Add(box.Id);
        ids[nameof(ShortageReasonCode)].Add(reason.Id);

        var component = new Item
        {
            Code = $"{token}-CMP",
            Name = $"{marker} Component",
            BaseUomId = pcs.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = Actor
        };
        var parent = new Item
        {
            Code = $"{token}-KIT",
            Name = $"{marker} Kit",
            BaseUomId = pcs.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = true,
            CreatedBy = Actor
        };
        var conversion = new UomConversion
        {
            FromUomId = box.Id,
            ToUomId = pcs.Id,
            Factor = 10m,
            RoundingMode = RoundingMode.None,
            IsActive = true,
            CreatedBy = Actor
        };

        dbContext.Items.AddRange(component, parent);
        dbContext.UomConversions.Add(conversion);
        await dbContext.SaveChangesAsync(cancellationToken);
        ids[nameof(Item)].Add(component.Id);
        ids[nameof(Item)].Add(parent.Id);
        ids[nameof(UomConversion)].Add(conversion.Id);

        var itemComponent = new ItemComponent
        {
            ItemId = parent.Id,
            ComponentItemId = component.Id,
            UomId = pcs.Id,
            Quantity = 2m,
            CreatedBy = Actor
        };
        dbContext.ItemComponents.Add(itemComponent);
        await dbContext.SaveChangesAsync(cancellationToken);
        ids[nameof(ItemComponent)].Add(itemComponent.Id);

        var po = await purchaseOrderService.CreateDraftAsync(
            new UpsertPurchaseOrderRequest(
                $"{token}-PO",
                supplier.Id,
                date,
                date.AddDays(3),
                marker,
                [
                    new UpsertPurchaseOrderLineRequest(1, parent.Id, 5m, 100m, box.Id, marker)
                ]),
            Actor,
            cancellationToken);
        po = await purchaseOrderPostingService.PostAsync(po.Id, Actor, cancellationToken)
            ?? throw new InvalidOperationException("Seed purchase order disappeared during posting.");
        ids[nameof(PurchaseOrder)].Add(po.Id);
        ids[nameof(PurchaseOrderLine)].Add(po.Lines.Single().Id);

        var receipt = await purchaseReceiptService.CreateDraftAsync(
            new UpsertPurchaseReceiptDraftRequest(
                $"{token}-PR",
                supplier.Id,
                warehouse.Id,
                po.Id,
                date.AddDays(1),
                0m,
                marker,
                [
                    new UpsertPurchaseReceiptLineRequest(
                        1,
                        po.Lines.Single().Id,
                        parent.Id,
                        5m,
                        5m,
                        box.Id,
                        marker,
                        [
                            new UpsertPurchaseReceiptLineComponentRequest(component.Id, 98m, pcs.Id, reason.Id, marker)
                        ])
                ]),
            Actor,
            cancellationToken);
        receipt = await purchaseReceiptPostingService.PostAsync(receipt.Id, Actor, cancellationToken)
            ?? throw new InvalidOperationException("Seed purchase receipt disappeared during posting.");
        ids[nameof(PurchaseReceipt)].Add(receipt.Id);
        ids[nameof(PurchaseReceiptLine)].Add(receipt.Lines.Single().Id);
        ids[nameof(PurchaseReceiptLineComponent)].Add(receipt.Lines.Single().Components.Single().Id);

        var shortage = await dbContext.ShortageLedgerEntries
            .AsNoTracking()
            .SingleAsync(entity => entity.PurchaseReceiptId == receipt.Id, cancellationToken);
        ids[nameof(ShortageLedgerEntry)].Add(shortage.Id);

        var physicalResolution = await shortageResolutionService.CreateDraftAsync(
            new UpsertShortageResolutionRequest(
                $"{token}-SRP",
                supplier.Id,
                ShortageResolutionType.Physical,
                date.AddDays(2),
                1m,
                null,
                "EGP",
                marker,
                [
                    new UpsertShortageResolutionAllocationRequest(shortage.Id, 1m, null, null, "Manual", 1)
                ]),
            Actor,
            cancellationToken);
        physicalResolution = await shortageResolutionPostingService.PostAsync(physicalResolution.Id, Actor, cancellationToken)
            ?? throw new InvalidOperationException("Seed physical shortage resolution disappeared during posting.");
        ids[nameof(ShortageResolution)].Add(physicalResolution.Id);
        ids[nameof(ShortageResolutionAllocation)].Add(physicalResolution.Allocations.Single().Id);

        var payment = await paymentService.CreateDraftAsync(
            new UpsertPaymentRequest(
                $"{token}-PAY",
                PaymentPartyType.Supplier,
                supplier.Id,
                PaymentDirection.OutboundToParty,
                250m,
                date.AddDays(3),
                "EGP",
                null,
                PaymentMethod.BankTransfer,
                $"{token}-BANK",
                marker,
                [
                    new UpsertPaymentAllocationRequest(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, null, 250m, 1)
                ]),
            Actor,
            cancellationToken);
        payment = await paymentPostingService.PostAsync(payment.Id, Actor, cancellationToken)
            ?? throw new InvalidOperationException("Seed payment disappeared during posting.");
        ids[nameof(Payment)].Add(payment.Id);
        ids[nameof(PaymentAllocation)].AddRange(payment.Allocations.Select(allocation => allocation.Id));

        var purchaseReturn = await purchaseReturnService.CreateDraftAsync(
            new UpsertPurchaseReturnRequest(
                $"{token}-RTN",
                supplier.Id,
                receipt.Id,
                date.AddDays(4),
                marker,
                [
                    new UpsertPurchaseReturnLineRequest(1, parent.Id, null, warehouse.Id, 1m, box.Id, receipt.Lines.Single().Id)
                ]),
            Actor,
            cancellationToken);
        purchaseReturn = await purchaseReturnPostingService.PostAsync(purchaseReturn.Id, Actor, cancellationToken)
            ?? throw new InvalidOperationException("Seed purchase return disappeared during posting.");
        ids[nameof(PurchaseReturn)].Add(purchaseReturn.Id);
        ids[nameof(PurchaseReturnLine)].AddRange(purchaseReturn.Lines.Select(line => line.Id));

        ids[nameof(StockLedgerEntry)].AddRange(await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(entity =>
                entity.SourceDocId == receipt.Id ||
                entity.SourceDocId == physicalResolution.Id ||
                entity.SourceDocId == purchaseReturn.Id)
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken));
        ids[nameof(SupplierStatementEntry)].AddRange(await dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity =>
                entity.SourceDocId == receipt.Id ||
                entity.SourceDocId == payment.Id ||
                entity.SourceDocId == purchaseReturn.Id)
            .Select(entity => entity.Id)
            .ToArrayAsync(cancellationToken));

        return ids;
    }

    private async Task<OrganizationTestDataCommandResult?> ValidateSafeCommandAsync(
        Guid organizationId,
        string profile,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(profile, SupportedProfile, StringComparison.OrdinalIgnoreCase))
        {
            return Rejected(organizationId, profile, string.Empty, false, $"Only the {SupportedProfile} profile is supported by this build.");
        }

        if (organizationId == Guid.Empty)
        {
            return Rejected(organizationId, profile, string.Empty, false, "Organization id is required.");
        }

        if (!IsSafeEnvironment(hostEnvironment.EnvironmentName))
        {
            return Rejected(organizationId, profile, string.Empty, false, $"Environment '{hostEnvironment.EnvironmentName}' is blocked for test data tooling.");
        }

        var organizationExists = await dbContext.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(entity => entity.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Rejected(organizationId, profile, string.Empty, false, "Organization was not found.");
        }

        return null;
    }

    private async Task<OrganizationDataSnapshot> BuildSnapshotAsync(
        Guid organizationId,
        string profile,
        string runId,
        CancellationToken cancellationToken)
    {
        var counts = await CountFullOrganizationScopeAsync(organizationId, cancellationToken);
        var stockBaseQuantities = await dbContext.StockLedgerEntries
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId)
            .Select(entity => entity.BaseQty)
            .ToListAsync(cancellationToken);
        var supplierStatementDebits = await dbContext.SupplierStatementEntries
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId)
            .Select(entity => entity.Debit)
            .ToListAsync(cancellationToken);
        var supplierStatementCredits = await dbContext.SupplierStatementEntries
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId)
            .Select(entity => entity.Credit)
            .ToListAsync(cancellationToken);
        var shortageOpenQuantities = await dbContext.ShortageLedgerEntries
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId)
            .Select(entity => entity.OpenQty)
            .ToListAsync(cancellationToken);

        var totals = new Dictionary<string, decimal>
        {
            ["stockBaseQty"] = stockBaseQuantities.Sum(),
            ["supplierStatementDebit"] = supplierStatementDebits.Sum(),
            ["supplierStatementCredit"] = supplierStatementCredits.Sum(),
            ["shortageOpenQty"] = shortageOpenQuantities.Sum()
        };

        return new OrganizationDataSnapshot(
            ManifestVersion,
            organizationId,
            profile,
            runId,
            DateTime.UtcNow,
            counts,
            totals);
    }

    private async Task<IReadOnlyDictionary<string, int>> CountFullOrganizationScopeAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return new Dictionary<string, int>
        {
            [nameof(Customer)] = await CountAsync(dbContext.Customers, organizationId, cancellationToken),
            [nameof(Supplier)] = await CountAsync(dbContext.Suppliers, organizationId, cancellationToken),
            [nameof(Warehouse)] = await CountAsync(dbContext.Warehouses, organizationId, cancellationToken),
            [nameof(Uom)] = await CountAsync(dbContext.Uoms, organizationId, cancellationToken),
            [nameof(UomConversion)] = await CountAsync(dbContext.UomConversions, organizationId, cancellationToken),
            [nameof(Item)] = await CountAsync(dbContext.Items, organizationId, cancellationToken),
            [nameof(ItemComponent)] = await CountAsync(dbContext.ItemComponents, organizationId, cancellationToken),
            [nameof(ShortageReasonCode)] = await CountAsync(dbContext.ShortageReasonCodes, organizationId, cancellationToken),
            [nameof(PurchaseOrder)] = await CountAsync(dbContext.PurchaseOrders, organizationId, cancellationToken),
            [nameof(PurchaseOrderLine)] = await CountAsync(dbContext.PurchaseOrderLines, organizationId, cancellationToken),
            [nameof(PurchaseReceipt)] = await CountAsync(dbContext.PurchaseReceipts, organizationId, cancellationToken),
            [nameof(PurchaseReceiptLine)] = await CountAsync(dbContext.PurchaseReceiptLines, organizationId, cancellationToken),
            [nameof(PurchaseReceiptLineComponent)] = await CountAsync(dbContext.PurchaseReceiptLineComponents, organizationId, cancellationToken),
            [nameof(PurchaseReturn)] = await CountAsync(dbContext.PurchaseReturns, organizationId, cancellationToken),
            [nameof(PurchaseReturnLine)] = await CountAsync(dbContext.PurchaseReturnLines, organizationId, cancellationToken),
            [nameof(StockLedgerEntry)] = await CountAsync(dbContext.StockLedgerEntries, organizationId, cancellationToken),
            [nameof(ShortageLedgerEntry)] = await CountAsync(dbContext.ShortageLedgerEntries, organizationId, cancellationToken),
            [nameof(ShortageResolution)] = await CountAsync(dbContext.ShortageResolutions, organizationId, cancellationToken),
            [nameof(ShortageResolutionAllocation)] = await CountAsync(dbContext.ShortageResolutionAllocations, organizationId, cancellationToken),
            [nameof(SupplierStatementEntry)] = await CountAsync(dbContext.SupplierStatementEntries, organizationId, cancellationToken),
            [nameof(Payment)] = await CountAsync(dbContext.Payments, organizationId, cancellationToken),
            [nameof(PaymentAllocation)] = await CountAsync(dbContext.PaymentAllocations, organizationId, cancellationToken)
        };
    }

    private async Task DeleteManifestScopeAsync(
        OrganizationTestDataManifest manifest,
        CancellationToken cancellationToken)
    {
        await DeleteIdsAsync(dbContext.PaymentAllocations, manifest, nameof(PaymentAllocation), cancellationToken);
        await DeleteIdsAsync(dbContext.Payments, manifest, nameof(Payment), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseReturnLines, manifest, nameof(PurchaseReturnLine), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseReturns, manifest, nameof(PurchaseReturn), cancellationToken);
        await DeleteIdsAsync(dbContext.ShortageResolutionAllocations, manifest, nameof(ShortageResolutionAllocation), cancellationToken);
        await DeleteIdsAsync(dbContext.ShortageResolutions, manifest, nameof(ShortageResolution), cancellationToken);
        await DeleteIdsAsync(dbContext.SupplierStatementEntries, manifest, nameof(SupplierStatementEntry), cancellationToken);
        await DeleteIdsAsync(dbContext.ShortageLedgerEntries, manifest, nameof(ShortageLedgerEntry), cancellationToken);
        await DeleteIdsAsync(dbContext.StockLedgerEntries, manifest, nameof(StockLedgerEntry), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseReceiptLineComponents, manifest, nameof(PurchaseReceiptLineComponent), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseReceiptLines, manifest, nameof(PurchaseReceiptLine), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseReceipts, manifest, nameof(PurchaseReceipt), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseOrderLines, manifest, nameof(PurchaseOrderLine), cancellationToken);
        await DeleteIdsAsync(dbContext.PurchaseOrders, manifest, nameof(PurchaseOrder), cancellationToken);
        await DeleteIdsAsync(dbContext.ItemComponents, manifest, nameof(ItemComponent), cancellationToken);
        await DeleteIdsAsync(dbContext.UomConversions, manifest, nameof(UomConversion), cancellationToken);
        await DeleteIdsAsync(dbContext.Items, manifest, nameof(Item), cancellationToken);
        await DeleteIdsAsync(dbContext.ShortageReasonCodes, manifest, nameof(ShortageReasonCode), cancellationToken);
        await DeleteIdsAsync(dbContext.Uoms, manifest, nameof(Uom), cancellationToken);
        await DeleteIdsAsync(dbContext.Warehouses, manifest, nameof(Warehouse), cancellationToken);
        await DeleteIdsAsync(dbContext.Customers, manifest, nameof(Customer), cancellationToken);
        await DeleteIdsAsync(dbContext.Suppliers, manifest, nameof(Supplier), cancellationToken);
    }

    private async Task DeleteFullOrganizationScopeAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await DeleteOrganizationAsync(dbContext.PaymentAllocations, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Payments, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseReturnLines, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseReturns, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.ShortageResolutionAllocations, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.ShortageResolutions, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.SupplierStatementEntries, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.ShortageLedgerEntries, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.StockLedgerEntries, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseReceiptLineComponents, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseReceiptLines, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseReceipts, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseOrderLines, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.PurchaseOrders, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.ItemComponents, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.UomConversions, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Items, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.ShortageReasonCodes, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Uoms, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Warehouses, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Customers, organizationId, cancellationToken);
        await DeleteOrganizationAsync(dbContext.Suppliers, organizationId, cancellationToken);
    }

    private static async Task<int> CountAsync<TEntity>(
        DbSet<TEntity> set,
        Guid organizationId,
        CancellationToken cancellationToken)
        where TEntity : OrganizationScopedAuditableEntity
    {
        return await set
            .IgnoreQueryFilters()
            .CountAsync(entity => entity.OrganizationId == organizationId, cancellationToken);
    }

    private static async Task DeleteOrganizationAsync<TEntity>(
        DbSet<TEntity> set,
        Guid organizationId,
        CancellationToken cancellationToken)
        where TEntity : OrganizationScopedAuditableEntity
    {
        await set
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task DeleteIdsAsync<TEntity>(
        DbSet<TEntity> set,
        OrganizationTestDataManifest manifest,
        string key,
        CancellationToken cancellationToken)
        where TEntity : OrganizationScopedAuditableEntity
    {
        if (!manifest.EntityIds.TryGetValue(key, out var ids) || ids.Count == 0)
        {
            return;
        }

        await set
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == manifest.OrganizationId && ids.Contains(entity.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private string GetManifestPath(string runId)
        => Path.Combine(GetToolingDirectory("seed-runs"), $"{runId}.manifest.json");

    private string GetSnapshotPath(string runId)
        => Path.Combine(GetToolingDirectory("snapshots"), $"{runId}.snapshot.json");

    private string GetToolingDirectory(string name)
    {
        var directory = Path.Combine(localStoragePathService.DataDirectory, "TestTooling", name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? localStoragePathService.DataDirectory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);
    }

    private static async Task<OrganizationTestDataManifest> ReadManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<OrganizationTestDataManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Seed manifest could not be parsed.");
    }

    private static async Task<OrganizationDataSnapshot> ReadSnapshotAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<OrganizationDataSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot could not be parsed.");
    }

    private static Dictionary<string, int> BuildManifestCounts(OrganizationTestDataManifest manifest)
        => manifest.EntityIds.ToDictionary(pair => pair.Key, pair => pair.Value.Count);

    private static IReadOnlyDictionary<string, int> BuildPlannedSeedCounts(ScaleDefinition scale)
    {
        return new Dictionary<string, int>
        {
            [nameof(Customer)] = scale.CustomerCount,
            [nameof(Supplier)] = 1,
            [nameof(Warehouse)] = 1,
            [nameof(Uom)] = 2,
            [nameof(UomConversion)] = 1,
            [nameof(Item)] = 2,
            [nameof(ItemComponent)] = 1,
            [nameof(ShortageReasonCode)] = 1,
            [nameof(PurchaseOrder)] = 1,
            [nameof(PurchaseReceipt)] = 1,
            [nameof(ShortageResolution)] = 1,
            [nameof(Payment)] = 1,
            [nameof(PurchaseReturn)] = 1
        };
    }

    private static IReadOnlyList<string> BuildResetWarnings(ResetOrganizationDataRequest request)
    {
        var warnings = new List<string>();
        if (!request.PreserveUsers)
        {
            warnings.Add("User/security deletion is intentionally unsupported by this reset tool; users are preserved.");
        }

        if (!request.PreserveOrganization)
        {
            warnings.Add("Organization shell deletion is intentionally unsupported by this reset tool; the organization row is preserved.");
        }

        if (!request.PreserveSettings)
        {
            warnings.Add("Organization/security settings deletion is intentionally unsupported by this reset tool; settings are preserved.");
        }

        if (request.SkipSafetyBackup && !request.TestDataOnly)
        {
            warnings.Add("Full organization reset never skips the safety backup.");
        }

        return warnings;
    }

    private static Dictionary<string, List<Guid>> NewIdMap()
    {
        return new Dictionary<string, List<Guid>>
        {
            [nameof(Customer)] = [],
            [nameof(Supplier)] = [],
            [nameof(Warehouse)] = [],
            [nameof(Uom)] = [],
            [nameof(UomConversion)] = [],
            [nameof(Item)] = [],
            [nameof(ItemComponent)] = [],
            [nameof(ShortageReasonCode)] = [],
            [nameof(PurchaseOrder)] = [],
            [nameof(PurchaseOrderLine)] = [],
            [nameof(PurchaseReceipt)] = [],
            [nameof(PurchaseReceiptLine)] = [],
            [nameof(PurchaseReceiptLineComponent)] = [],
            [nameof(PurchaseReturn)] = [],
            [nameof(PurchaseReturnLine)] = [],
            [nameof(StockLedgerEntry)] = [],
            [nameof(ShortageLedgerEntry)] = [],
            [nameof(ShortageResolution)] = [],
            [nameof(ShortageResolutionAllocation)] = [],
            [nameof(SupplierStatementEntry)] = [],
            [nameof(Payment)] = [],
            [nameof(PaymentAllocation)] = []
        };
    }

    private static ScaleDefinition? NormalizeScale(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "small" => new ScaleDefinition("small", 2),
            "medium" => new ScaleDefinition("medium", 5),
            "large" => new ScaleDefinition("large", 10),
            _ => null
        };
    }

    private static string BuildMarker(Guid organizationId, int seed)
        => $"{MarkerPrefix}-{organizationId.ToString("N")[..8]}-{seed}";

    private static string BuildCodeToken(Guid organizationId, int seed)
        => $"HCRS-{organizationId.ToString("N")[..6]}-{seed}";

    private static string BuildRunId(Guid organizationId, string profile, int seed)
        => $"{profile}-{organizationId.ToString("N")[..8]}-{seed}";

    private static bool IsSafeEnvironment(string environmentName)
        => string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(environmentName, "Desktop", StringComparison.OrdinalIgnoreCase);

    private static OrganizationTestDataCommandResult Rejected(
        Guid organizationId,
        string profile,
        string runId,
        bool dryRun,
        string message)
    {
        return new OrganizationTestDataCommandResult(
            OrganizationTestDataCommandStatus.Rejected,
            message,
            organizationId,
            profile,
            runId,
            dryRun,
            null,
            null,
            null,
            new Dictionary<string, int>(),
            []);
    }

    private static void TryDelete(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record ScaleDefinition(string Name, int CustomerCount);
}
