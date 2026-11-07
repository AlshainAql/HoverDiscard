using HoverDiscard.Core;
using Xunit;

namespace HoverDiscard.Tests;

public sealed class DiscardBatchTests
{
    private static readonly ItemKey Normal = new(100, ItemQuality.Normal);
    private static readonly ItemKey High = new(100, ItemQuality.High);
    private static readonly ItemKey Other = new(200, ItemQuality.Normal);
    private static SlotState Slot(int position, ItemKey key, int quantity = 1, string fingerprint = "original") =>
        new(new SlotAddress(0, position), key, quantity, fingerprint, true);
    private static InventorySnapshot Snapshot(params SlotState[] slots) => new(42, 7, slots);
    private static TimeSpan At(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void ConfirmsEachStackBeforeSendingTheNextAndRemembersOnlyAfterSuccess()
    {
        var first = Slot(0, Normal, 99);
        var second = Slot(1, Normal, 20);
        var snapshot = Snapshot(first, second, Slot(2, High));
        var learned = new List<ItemKey>();
        var completed = new List<ItemKey>();
        var requests = new List<SlotAddress>();
        var batch = new DiscardBatch(snapshot, new HashSet<ItemKey> { Normal },
            new HashSet<ItemKey> { Normal }, true, learned.Add, completed.Add);

        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), snapshot, true, requests.Add);
        Assert.Single(requests);
        Assert.Empty(learned);
        Assert.Empty(completed);

        var afterFirst = snapshot with { Slots = [SlotState.Empty(first.Address), second, Slot(2, High)] };
        batch.Tick(At(2), afterFirst, true, requests.Add);
        Assert.Equal(2, requests.Count);
        Assert.Single(learned);
        Assert.Empty(completed);

        var afterSecond = afterFirst with { Slots = [SlotState.Empty(first.Address), SlotState.Empty(second.Address), Slot(2, High)] };
        batch.Tick(At(3), afterSecond, true, requests.Add);
        Assert.Equal(BatchResult.Completed, batch.Result);
        Assert.Equal(2, batch.CompletedStacks);
        Assert.Equal([Normal], completed);
        Assert.DoesNotContain(High, learned);
        Assert.Equal([first.Address, second.Address], requests);
    }

    [Fact]
    public void RememberOffStillExecutesSavedAndNewItemsWithoutLearning()
    {
        var snapshot = Snapshot(Slot(0, Normal), Slot(1, Other));
        var learned = new List<ItemKey>();
        var batch = new DiscardBatch(snapshot, new HashSet<ItemKey> { Normal, Other },
            new HashSet<ItemKey> { Other }, false, learned.Add, _ => { });
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        snapshot = snapshot with { Slots = [SlotState.Empty(new(0, 0)), Slot(1, Other)] };
        batch.Tick(At(1), snapshot, true, requests.Add);
        snapshot = snapshot with { Slots = [SlotState.Empty(new(0, 0)), SlotState.Empty(new(0, 1))] };
        batch.Tick(At(2), snapshot, true, requests.Add);
        Assert.Equal(BatchResult.Completed, batch.Result);
        Assert.Equal(2, requests.Count);
        Assert.Empty(learned);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("quality")]
    [InlineData("fingerprint")]
    [InlineData("new-item")]
    [InlineData("moved")]
    public void ChangesBeforeFirstRequestPreventAllDiscarding(string change)
    {
        var snapshot = Snapshot(Slot(0, Normal, 99), SlotState.Empty(new(0, 1)));
        var changed = change switch
        {
            "quantity" => Snapshot(Slot(0, Normal, 100), SlotState.Empty(new(0, 1))),
            "quality" => Snapshot(Slot(0, High, 99), SlotState.Empty(new(0, 1))),
            "fingerprint" => Snapshot(Slot(0, Normal, 99, "modified"), SlotState.Empty(new(0, 1))),
            "new-item" => Snapshot(Slot(0, Normal, 99), Slot(1, Other)),
            _ => Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Normal, 99)),
        };
        var batch = Create(snapshot);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), changed, true, requests.Add);
        Assert.Equal(BatchResult.InventoryChanged, batch.Result);
        Assert.Empty(requests);
    }

    [Fact]
    public void APartialQuantityChangeIsNotAcceptedAsSuccessfulDiscard()
    {
        var snapshot = Snapshot(Slot(0, Normal, 99));
        var learned = new List<ItemKey>();
        var batch = Create(snapshot, learned);
        batch.Tick(At(0), snapshot, true, _ => { });
        batch.Tick(At(1), Snapshot(Slot(0, Normal, 98)), true, _ => { });
        Assert.Equal(BatchResult.InventoryChanged, batch.Result);
        Assert.Empty(learned);
    }

    [Fact]
    public void UnrelatedChangesDuringRequestPreventLearningAndFurtherRequests()
    {
        var snapshot = Snapshot(Slot(0, Normal), Slot(1, Other));
        var learned = new List<ItemKey>();
        var batch = Create(snapshot, learned);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Other, 2)), true, requests.Add);
        Assert.Equal(BatchResult.InventoryChanged, batch.Result);
        Assert.Single(requests);
        Assert.Empty(learned);
    }

    [Fact]
    public void TimeoutNeverRetriesOrLearnsAnUnconfirmedRequest()
    {
        var snapshot = Snapshot(Slot(0, Normal));
        var learned = new List<ItemKey>();
        var batch = Create(snapshot, learned);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(4.99), snapshot, true, requests.Add);
        Assert.True(batch.IsRunning);
        batch.Tick(At(5), snapshot, true, requests.Add);
        batch.Tick(At(6), Snapshot(SlotState.Empty(new(0, 0))), true, requests.Add);
        Assert.Equal(BatchResult.TimedOut, batch.Result);
        Assert.Single(requests);
        Assert.Empty(learned);
    }

    [Fact]
    public void StopBeforeFirstRequestSendsNothing()
    {
        var snapshot = Snapshot(Slot(0, Normal));
        var batch = Create(snapshot);
        var requests = new List<SlotAddress>();
        batch.Stop();
        batch.Tick(At(0), snapshot, true, requests.Add);
        Assert.Equal(BatchResult.Stopped, batch.Result);
        Assert.Empty(requests);
    }

    [Fact]
    public void StopReconcilesPendingRequestAndKeepsPartialItemSelected()
    {
        var snapshot = Snapshot(Slot(0, Normal, 99), Slot(1, Normal, 20));
        var learned = new List<ItemKey>();
        var completed = new List<ItemKey>();
        var batch = new DiscardBatch(snapshot, new HashSet<ItemKey> { Normal },
            new HashSet<ItemKey> { Normal }, true, learned.Add, completed.Add);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Stop();
        batch.Tick(At(1), snapshot, true, requests.Add);
        Assert.True(batch.IsRunning);
        batch.Tick(At(2), Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Normal, 20)), true, requests.Add);
        Assert.Equal(BatchResult.Stopped, batch.Result);
        Assert.Single(requests);
        Assert.Equal([Normal], learned);
        Assert.Empty(completed);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("character")]
    [InlineData("territory")]
    [InlineData("unavailable")]
    public void ContextChangesStopWithoutLearning(string change)
    {
        var snapshot = Snapshot(Slot(0, Normal));
        InventorySnapshot? changed = change switch
        {
            "logout" => snapshot with { ContentId = 0 },
            "character" => snapshot with { ContentId = 999 },
            "territory" => snapshot with { TerritoryId = 8 },
            _ => null,
        };
        var learned = new List<ItemKey>();
        var batch = Create(snapshot, learned);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), changed, true, requests.Add);
        Assert.Equal(BatchResult.ContextChanged, batch.Result);
        Assert.Single(requests);
        Assert.Empty(learned);
    }

    [Fact]
    public void LosingFocusPreventsTheNextRequestEvenIfFocusReturns()
    {
        var snapshot = Snapshot(Slot(0, Normal), Slot(1, Normal));
        var batch = Create(snapshot);
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), snapshot, false, requests.Add);
        batch.Tick(At(2), Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Normal)), true, requests.Add);
        Assert.Equal(BatchResult.Stopped, batch.Result);
        Assert.Single(requests);
    }

    [Fact]
    public void FailedRequestsDoNotRetryOrLearn()
    {
        var snapshot = Snapshot(Slot(0, Normal));
        var learned = new List<ItemKey>();
        var batch = Create(snapshot, learned);
        batch.Tick(At(0), snapshot, true, _ => throw new InvalidOperationException());
        batch.Tick(At(1), snapshot, true, _ => Assert.Fail("Retried a failed request."));
        Assert.Equal(BatchResult.RequestFailed, batch.Result);
        Assert.Empty(learned);
    }

    [Fact]
    public void SelectionAndRememberToggleAreCapturedAtStart()
    {
        var snapshot = Snapshot(Slot(0, Normal), Slot(1, Other));
        var included = new HashSet<ItemKey> { Normal };
        var fresh = new HashSet<ItemKey> { Normal };
        var learned = new List<ItemKey>();
        var batch = new DiscardBatch(snapshot, included, fresh, true, learned.Add, _ => { });
        included.Clear();
        included.Add(Other);
        fresh.Clear();
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Other)), true, requests.Add);
        Assert.Equal([new SlotAddress(0, 0)], requests);
        Assert.Equal([Normal], learned);
    }

    [Fact]
    public void UncheckedAndIndisposableItemsAreNeverRequested()
    {
        var snapshot = Snapshot(Slot(0, Normal) with { CanDiscard = false }, Slot(1, Other), Slot(2, High));
        var batch = new DiscardBatch(snapshot, new HashSet<ItemKey> { Normal, High },
            new HashSet<ItemKey>(), true, _ => { }, _ => { });
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        Assert.Equal([new SlotAddress(0, 2)], requests);
    }

    [Fact]
    public void PersistenceCallbackCanStopBeforeTheNextRequest()
    {
        var snapshot = Snapshot(Slot(0, Normal), Slot(1, Normal));
        DiscardBatch? batch = null;
        batch = new DiscardBatch(snapshot, new HashSet<ItemKey> { Normal },
            new HashSet<ItemKey> { Normal }, true, _ => batch!.Stop(), _ => { });
        var requests = new List<SlotAddress>();
        batch.Tick(At(0), snapshot, true, requests.Add);
        batch.Tick(At(1), Snapshot(SlotState.Empty(new(0, 0)), Slot(1, Normal)), true, requests.Add);
        Assert.Equal(BatchResult.Stopped, batch.Result);
        Assert.Equal(1, batch.CompletedStacks);
        Assert.Single(requests);
    }

    private static DiscardBatch Create(InventorySnapshot snapshot, List<ItemKey>? learned = null) =>
        new(snapshot, new HashSet<ItemKey> { Normal }, new HashSet<ItemKey> { Normal }, true,
            key => learned?.Add(key), _ => { });
}
