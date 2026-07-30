using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public class BTreeTests
{
    [Test]
    public void PublicInterfaceShouldExposeIndexOperations()
    {
        IBPlusTree<int, string> tree = new BalancingTreeService<int, string>(4);

        tree.Add(20, "twenty");
        tree.Add(10, "ten");

        tree.Order.Should().Be(4);
        tree.Count.Should().Be(2);
        tree.ContainsKey(10).Should().BeTrue();
        tree.TryGetValue(20, out var value).Should().BeTrue();
        value.Should().Be("twenty");
    }

    [Test]
    public void DuplicateKeysShouldRetainEveryValue()
    {
        var tree = new BalancingTreeService<int, string>(4);

        tree.Add(10, "first");
        tree.Add(5, "five");
        tree.Add(10, "second");
        tree.Add(10, "third");

        tree.Find(10).Should().Equal("first", "second", "third");
        tree.Scan().Select(entry => entry.Key).Should().Equal(5, 10, 10, 10);
        tree.Count.Should().Be(4);
        ValidateTree(tree);
    }

    [Test]
    public void RemoveShouldTargetASpecificKeyValuePair()
    {
        var tree = new BalancingTreeService<int, string>(4);
        tree.Add(10, "first");
        tree.Add(10, "second");

        tree.Remove(10, "second").Should().BeTrue();
        tree.Remove(10, "missing").Should().BeFalse();

        tree.Find(10).Should().Equal("first");
        tree.Count.Should().Be(1);
    }

    [Test]
    public void KeyAndValueComparersShouldControlTheirRespectiveOperations()
    {
        var tree = new BalancingTreeService<string, string>(
            4,
            StringComparer.OrdinalIgnoreCase,
            StringComparer.OrdinalIgnoreCase);

        tree.Add("Customer", "ROW-1");

        tree.ContainsKey("customer").Should().BeTrue();
        tree.Remove("CUSTOMER", "row-1").Should().BeTrue();
    }

    [Test]
    public void BoundsShouldReturnEntriesRatherThanKeysAlone()
    {
        var tree = CreateTree((10, "ten"), (20, "twenty"), (30, "thirty"));

        tree.TryGetLowerBound(15, out var lower).Should().BeTrue();
        lower.Should().Be(new BTreeEntry<int, string>(20, "twenty"));

        tree.TryGetUpperBound(20, out var upper).Should().BeTrue();
        upper.Should().Be(new BTreeEntry<int, string>(30, "thirty"));

        tree.TryGetUpperBound(30, out _).Should().BeFalse();
    }

    [Test]
    public void RangeScansShouldRespectBoundsAndDirection()
    {
        var tree = CreateTree(
            (5, "five"),
            (10, "ten-a"),
            (10, "ten-b"),
            (15, "fifteen"),
            (20, "twenty"));

        tree.Scan(new BTreeRange<int>(10, 20, IncludeUpperBound: false))
            .Select(entry => entry.Value)
            .Should().Equal("ten-a", "ten-b", "fifteen");

        tree.Scan(new BTreeRange<int>(10, 20, Direction: ScanDirection.Descending))
            .Select(entry => entry.Key)
            .Should().Equal(20, 15, 10, 10);
    }

    [TestCase(3, 271)]
    [TestCase(4, 619)]
    [TestCase(5, 1451)]
    [TestCase(6, 2389)]
    public void RandomRemovalShouldPreserveTreeInvariants(int order, int seed)
    {
        var tree = new BalancingTreeService<int, string>(order);
        var keys = Enumerable.Range(1, 200).ToArray();
        var removalOrder = keys.ToArray();
        new Random(seed).Shuffle(removalOrder);
        var expected = keys.ToList();

        foreach (var key in keys)
            tree.Add(key, $"row-{key}");

        foreach (var key in removalOrder)
        {
            tree.Remove(key, $"row-{key}").Should().BeTrue();
            expected.Remove(key);
            tree.Scan().Select(entry => entry.Key).Should().Equal(expected);
            ValidateTree(tree);
        }

        tree.BalancingTree.Root.Should().BeOfType<BalancingTreeLeafNode<int, string>>();
        tree.Count.Should().Be(0);
    }

    [TestCase(3, 367)]
    [TestCase(4, 821)]
    [TestCase(5, 1597)]
    [TestCase(6, 2551)]
    public void InterleavedOperationsWithDuplicateKeysShouldRemainValid(int order, int seed)
    {
        var tree = new BalancingTreeService<int, int>(order);
        var expected = new List<BTreeEntry<int, int>>();
        var random = new Random(seed);
        var nextValue = 0;

        for (var operation = 0; operation < 500; operation++)
        {
            var key = random.Next(1, 31);

            if (random.Next(2) == 0)
            {
                var entry = new BTreeEntry<int, int>(key, nextValue++);
                tree.Add(entry.Key, entry.Value);
                expected.Add(entry);
            }
            else
            {
                var entry = expected.FirstOrDefault(candidate => candidate.Key == key);
                var exists = expected.Contains(entry);

                tree.Remove(key, entry.Value).Should().Be(exists);
                if (exists)
                    expected.Remove(entry);
            }

            tree.Scan().Select(entry => entry.Key)
                .Should().Equal(expected.Select(entry => entry.Key).Order());
            tree.Count.Should().Be(expected.Count);
            ValidateTree(tree);
        }
    }

    [Test]
    public void RootSplitShouldCreateDistinctLeafAndInternalNodeTypes()
    {
        var tree = new BalancingTreeService<int, string>(3);

        tree.BalancingTree.Root.Should().BeOfType<BalancingTreeLeafNode<int, string>>();

        tree.Add(10, "ten");
        tree.Add(20, "twenty");
        tree.Add(30, "thirty");

        var root = tree.BalancingTree.Root
            .Should().BeOfType<BalancingTreeInternalNode<int, string>>().Subject;
        root.Children.Should().AllBeOfType<BalancingTreeLeafNode<int, string>>();
        root.Keys.Should().Equal(30);
    }

    private static BalancingTreeService<int, string> CreateTree(
        params (int Key, string Value)[] entries)
    {
        var tree = new BalancingTreeService<int, string>(4);

        foreach (var entry in entries)
            tree.Add(entry.Key, entry.Value);

        return tree;
    }

    private static void ValidateTree<TKey, TValue>(
        BalancingTreeService<TKey, TValue> tree)
    {
        var comparer = Comparer<TKey>.Default;
        var leafDepths = new List<int>();
        var leaves = new List<BalancingTreeLeafNode<TKey, TValue>>();

        ValidateNode(
            tree.BalancingTree.Root,
            tree.Order,
            comparer,
            true,
            0,
            leafDepths,
            leaves);

        leafDepths.Distinct().Should().ContainSingle();

        for (var index = 0; index < leaves.Count; index++)
        {
            leaves[index].Previous.Should().BeSameAs(index == 0 ? null : leaves[index - 1]);
            leaves[index].Next.Should().BeSameAs(index == leaves.Count - 1 ? null : leaves[index + 1]);
        }
    }

    private static void ValidateNode<TKey, TValue>(
        BalancingTreeNode<TKey, TValue> node,
        int order,
        IComparer<TKey> comparer,
        bool isRoot,
        int depth,
        List<int> leafDepths,
        List<BalancingTreeLeafNode<TKey, TValue>> leaves)
    {
        node.Keys.Should().BeInAscendingOrder(comparer);
        node.Keys.Count.Should().BeLessThan(order);

        if (node is BalancingTreeLeafNode<TKey, TValue> leaf)
        {
            if (!isRoot)
                leaf.Entries.Count.Should().BeGreaterThanOrEqualTo(order / 2);

            leaf.Children.Should().BeEmpty();
            leafDepths.Add(depth);
            leaves.Add(leaf);
            return;
        }

        var internalNode = node.Should()
            .BeOfType<BalancingTreeInternalNode<TKey, TValue>>().Subject;
        internalNode.Children.Count.Should().Be(internalNode.Keys.Count + 1);

        if (isRoot)
            internalNode.Children.Count.Should().BeGreaterThanOrEqualTo(2);
        else
            internalNode.Children.Count.Should().BeGreaterThanOrEqualTo((order + 1) / 2);

        for (var index = 0; index < internalNode.Children.Count; index++)
        {
            var child = internalNode.Children[index];
            child.Parent.Should().BeSameAs(internalNode);
            ValidateNode(child, order, comparer, false, depth + 1, leafDepths, leaves);

            if (index > 0)
            {
                comparer.Compare(internalNode.Keys[index - 1], GetMinimumKey(child))
                    .Should().Be(0);
            }
        }
    }

    private static TKey GetMinimumKey<TKey, TValue>(
        BalancingTreeNode<TKey, TValue> node)
    {
        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
            node = internalNode.Children[0];

        return ((BalancingTreeLeafNode<TKey, TValue>)node).Entries[0].Key;
    }
}
