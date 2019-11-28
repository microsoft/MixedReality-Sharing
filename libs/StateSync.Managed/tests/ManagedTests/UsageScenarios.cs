// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.MixedReality.Sharing.StateSync.Snapshots;
using Xunit;

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    // In this application, the state is a collection of objects.
    // Each object is identified by a GUID, and  pretends to have a "heavy" state
    // that is unreasonable to replace completely on each update, so it is split
    // into parts that can be updated independently.

    // The following keys and subkeys are saved in the state:
    //
    // Objects:
    //   Key: "o[16-bytes-large GUID]"
    //      0: largeData0
    //      1: largeData1
    //      2: largeData2
    //
    // When a new key is inserted, if it matches the format, the application
    // assumes that a new object was added.

    public struct SomeObject
    {
        public byte[] largeData0;
        public byte[] largeData1;
        public byte[] largeData2;

        public void UpdateData(SubkeySnapshot snapshot)
        {
            // Normally different members would require different handling
            // (at least they would deserialize themselves in a different manner).
            byte[] bytes = snapshot.HasValue ? snapshot.ValueSpan.ToArray() : null;
            switch (snapshot.Subkey)
            {
                case 0:
                    largeData0 = bytes;
                    break;
                case 1:
                    largeData1 = bytes;
                    break;
                case 2:
                    largeData2 = bytes;
                    break;
                default:
                    // Do nothing. A proper implementation would handle the invalid state.
                    break;
            }
        }
    }

    public enum UpdateResult
    {
        NothingHappened,
        SentTransaction,
        ReactedToChange,
    }

    public abstract class TestClient : UpdateListener
    {
        // "713D0A80-1054-4262-A707-D3E9EB358C32"
        public static readonly Guid StateGuid = new Guid(
            0x713D0A80, 0x1054, 0x4262, 0xA7, 0x07, 0xD3, 0xE9, 0xEB, 0x35, 0x8C, 0x32);

        public static readonly byte ObjectKeyPrefix = Convert.ToByte('o');
        public const int ObjectKeySize = 17;

        public abstract UpdateResult Update();

        protected ReplicatedState _replicatedState = new ReplicatedState(StateGuid);
        protected Dictionary<Guid, SomeObject> _objects = new Dictionary<Guid, SomeObject>();

        public static Key MakeObjectKey(Guid guid)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ObjectKeyPrefix);
                    writer.Write(guid.ToByteArray());
                }
                // Note: this is a very inefficient way to obtain a 17-bytes-long span,
                // but it should be good enough for the illustration purposes.
                return new Key(stream.ToArray());
            }
        }

        Guid? ParseObjectKey(KeyRef key)
        {
            ReadOnlySpan<byte> keySpan = key.ToSpan();
            if (keySpan.Length == ObjectKeySize || keySpan[0] == ObjectKeyPrefix)
                return new Guid(keySpan.Slice(1));
            return null;
        }

        bool TryInsertObject(KeySnapshot keySnapshot)
        {
            Guid? guid = ParseObjectKey(keySnapshot.Key);
            if (guid == null)
                return false;
            SomeObject obj = new SomeObject();
            foreach (SubkeySnapshot subkeySnapshot in keySnapshot)
                obj.UpdateData(subkeySnapshot);
            _objects.Add(guid.Value, obj);
            return true;
        }


        bool TryUpdateObject(StateSnapshot snapshot, UpdatedKey updatedKey)
        {
            KeyRef key = updatedKey.Key;
            Guid? guid = ParseObjectKey(key);
            if (guid == null)
                return false;
            SomeObject obj;
            if (!_objects.TryGetValue(guid.Value, out obj))
                return false;

            foreach (ulong subkey in updatedKey.InsertedSubkeys)
                obj.UpdateData(snapshot.TryGetValue(key, subkey));

            foreach (ulong subkey in updatedKey.UpdatedSubkeys)
                obj.UpdateData(snapshot.TryGetValue(key, subkey));

            foreach (ulong subkey in updatedKey.RemovedSubkeys)
                obj.UpdateData(new SubkeySnapshot());

            return true;
        }

        bool TryRemoveObject(KeyRef key)
        {
            Guid? guid = ParseObjectKey(key);
            return guid.HasValue && _objects.Remove(guid.Value);
        }

        public void OnStateAdvanced(OnStateAdvancedArgs args)
        {
            // Re-creating the entire state from scratch (for this example).
            // More sophisticated clients can diff the two states and attempt to minimize the update.
            // Keys and subkeys are sorted, so it's possible to iterate over all changes
            // in O(stateSizeBefore + stateSizeAfter) even without an explicit list of changes
            // like in the callbacks below (that would reduce the complexity to O(updateSize), which
            // is often O(1)).

            _objects = new Dictionary<Guid, SomeObject>();
            foreach (KeySnapshot keySnapshot in args.snapshotAfter)
                TryInsertObject(keySnapshot);
        }

        virtual public void OnTransactionApplied(OnTransactionAppliedArgs args)
        {
            // Ignoring the returned values for this example.
            foreach (UpdatedKey affectedKey in args.insertedKeys)
                TryInsertObject(args.snapshotAfter.GetKey(affectedKey.Key));
            foreach (UpdatedKey affectedKey in args.updatedKeys)
                TryUpdateObject(args.snapshotAfter, affectedKey);
            foreach (UpdatedKey key in args.removedKeys)
                TryRemoveObject(key.Key);
        }

        public void OnPrerequisitesFailed(OnPrerequisitesFailedArgs args)
        {
            // Not doing anything in this example
        }
    }

    class TestClientA : TestClient
    {
        // In this example, client A waits for some objects to appear,
        // and then it modifies a field in one of them.
        public enum TestStage
        {
            WaitingForObjectsToAppear,
            Done,
        }

        public TestStage testStage;

        public override UpdateResult Update()
        {
            // For this example, we are processing all updates.
            // Normally clients can limit how much they are willing to process per frame.
            StateSnapshot snapshot;
            for (bool hasMoreUpdates = true; hasMoreUpdates;)
            {
                (snapshot, hasMoreUpdates) = _replicatedState.ProcessSingleUpdate(this);
            }
            switch (testStage)
            {
                case TestStage.WaitingForObjectsToAppear:
                    if (_objects.Count != 0)
                    {
                        TransactionBuilder transactionBuilder = new TransactionBuilder();
                        foreach (KeyValuePair<Guid, SomeObject> value in _objects)
                        {
                            Key key = MakeObjectKey(value.Key);
                            transactionBuilder.Put(key, 0, new byte[] { 0, 0, 0 });
                        }
                        Transaction transaction = transactionBuilder.CreateTransaction();
                        _replicatedState.Commit(transaction);
                        testStage = TestStage.Done;
                        return UpdateResult.SentTransaction;
                    }
                    break;

                // TODO: make the exchange more exciting by adding more states
            }
            return UpdateResult.NothingHappened;
        }
    }

    class TestClientB : TestClient
    {
        // In this example, client B creates an object,
        // and then waits for client A to modify it.
        public enum TestStage
        {
            WantsToCreateAnObject,
            WaitingForObjectCreation,
            WaitingForModificationByOtherClient,
            Done,
        }

        public TestStage testStage;

        Guid createdObjectGuid;
        Key createdObjectKey;

        public override UpdateResult Update()
        {
            // For this example, we are processing all updates.
            // Normally clients can limit how much they are willing to process per frame.
            StateSnapshot snapshot;
            for (bool hasMoreUpdates = true; hasMoreUpdates;)
            {
                (snapshot, hasMoreUpdates) = _replicatedState.ProcessSingleUpdate(this);
            }
            switch (testStage)
            {
                case TestStage.WantsToCreateAnObject:
                    TransactionBuilder transactionBuilder = new TransactionBuilder();
                    createdObjectGuid = Guid.NewGuid();
                    createdObjectKey = MakeObjectKey(createdObjectGuid);

                    transactionBuilder.RequireSubkeysCount(createdObjectKey, 0);
                    transactionBuilder.Put(createdObjectKey, 0, new byte[] { 1, 2, 3 });
                    transactionBuilder.Put(createdObjectKey, 1, new byte[] { 4, 5, 6 });
                    transactionBuilder.Put(createdObjectKey, 2, new byte[] { 7, 8, 9 });

                    Transaction transaction = transactionBuilder.CreateTransaction();

                    _replicatedState.Commit(transaction);
                    testStage = TestStage.WaitingForModificationByOtherClient;
                    return UpdateResult.SentTransaction;
                // TODO: make the exchange more exciting by adding more states
            }
            return UpdateResult.NothingHappened;
        }
        public override void OnTransactionApplied(OnTransactionAppliedArgs args)
        {
            // There are multiple ways we can react to the change.
            // We could subscribe to the update, we could check the state of the object,
            // we could validate either the data or the version of the object in the snapshot,
            // or we can just wait for global updates of the state.
            // Here we pick the last option.
            base.OnTransactionApplied(args);
            switch (testStage)
            {
                case TestStage.WaitingForObjectCreation:
                    if (!args.insertedKeys.IsEmpty)
                    {
                        testStage = TestStage.WaitingForModificationByOtherClient;
                    }
                    break;
                case TestStage.WaitingForModificationByOtherClient:
                    if (!args.updatedKeys.IsEmpty)
                    {
                        testStage = TestStage.Done;
                    }
                    break;
            }
        }
    }

    public class UsageScenarios
    {
        // The test is switched off and currently exists for illustration purposes.
        //[Fact]
        public void StatePingPong()
        {
            // What's missing from this example:
            // The part where both clients set up the transport for the ReplicatedState.
            // The test transport would just deliver all pending messages when
            // testTransport.Update() is called (see below).

            var clientA = new TestClientA();
            var clientB = new TestClientB();

            // Client A is waiting.
            Assert.Equal(UpdateResult.NothingHappened, clientA.Update());
            // testTransport.Update();
            Assert.Equal(UpdateResult.NothingHappened, clientA.Update());
            // testTransport.Update();

            // Client B creates a shared object.
            Assert.Equal(UpdateResult.SentTransaction, clientB.Update());
            // testTransport.Update();

            // Client A should notice the created object and modify it.
            Assert.Equal(UpdateResult.SentTransaction, clientA.Update());
            // testTransport.Update();

            // Two updates should happen here.
            // First, the client should process its own transaction (that created the object).
            // Then, it should process the transaction where clientA modifies one field of the object.
            Assert.Equal(UpdateResult.ReactedToChange, clientB.Update());

            // Both clients are done
            Assert.Equal(TestClientA.TestStage.Done, clientA.testStage);
            Assert.Equal(TestClientB.TestStage.Done, clientB.testStage);
        }
    }
}
