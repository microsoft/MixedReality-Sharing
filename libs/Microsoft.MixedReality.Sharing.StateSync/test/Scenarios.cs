using System;
using System.Collections.Generic;
using Xunit;

#pragma warning disable CS0169

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    public interface ISyncData
    {
        void mutate(Random rand);
    }

    public class ScenarioBuiltin : ISyncData
    {
        int state_ = 10;

        public void mutate(Random rand) { state_ += rand.Next(1, 5); }
    }

    public class ScenarioBuiltin2 : ISyncData
    {
        string state_ = "Hello world!";

        public void mutate(Random rand)
        {
            state_ += String.Format("-{0}", (char)rand.Next('a', 'z'));
        }
    }

    public class ScenarioArrayBuiltin : ISyncData
    {
        public struct State
        {
            public List<int> moves_;
        }
        State state_ = new State
        {
            moves_ = new List<int> { 0, 1, 2, 3, 4, 5 }
        };

        public void mutate(Random rand)
        {
            int move = rand.Next(0, state_.moves_.Count);
            if (move == state_.moves_.Count)
            {
                state_.moves_.Add(rand.Next(-100, -1));
            }
            else
            {
                state_.moves_[move] = rand.Next(0, 100);
            }
        }
    }

    public class ScenarioArrayStruct : ISyncData
    {
        public struct Move
        {
            public string name_;
            public string comment_;
        }
        public struct State
        {
            public int current_;
            public List<Move> moves_;
        }
        State state_ = new State
        {
            current_ = 0,
            moves_ = new List<Move>{
                new Move{ name_="e4", comment_="!"},
                new Move{ name_= "e5", comment_="b" } }
        };

        public void mutate(Random rand)
        {
            if (rand.Next(0, 10) == 0 || state_.current_ != state_.moves_.Count - 1)
            {
                state_.current_ += Math.Clamp(rand.Next(-1, 1), 0, state_.moves_.Count - 1);
            }
            else
            {
                string n = "";
                n += (char)rand.Next('a', 'h');
                n += (char)rand.Next('1', '8');
                state_.moves_.Add(new Move{ name_ = n, comment_ = "?!"} );
            }
        }
    }

    public class ScenarioPointers : ISyncData
    {
        class Node
        {
            string value_;
            Node left_;
            Node right_;
        }

        struct State
        {
            string name_;
            Node root_;
        }

        public void mutate(Random rand)
        {
            // TODO
        }
    }

    public class Scenarios
    {
        Random random_ = new Random();

        public static TheoryData<ISyncData> SyncData =>
            new TheoryData<ISyncData>
            {
                new ScenarioBuiltin(),
                new ScenarioBuiltin2(),
                new ScenarioArrayBuiltin(),
                new ScenarioArrayStruct(),
                new ScenarioPointers(),
            };

        [Theory, MemberData(nameof(SyncData))]
        public void SyncTest(ISyncData local)
        {
            // create N 'remote's
            local.mutate(random_);
            // sync with remotes
        }
    }
}
