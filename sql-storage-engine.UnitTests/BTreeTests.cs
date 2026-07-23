using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }
    
    [Test]
    public void WhenLeftChildSplits_ThenSiblingShouldBeInsertedImmediatelyAfterIt_OrderFour()
    {
        var service = new BalancingTreeService(4);

        service.Add(10);
        service.Add(20);
        service.Add(30);
        service.Add(40);
        service.Add(50);
        service.Add(60);
        service.Add(5);
        service.Add(6);
        service.Add(7);
        service.Add(8);

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
        {
            Values = [7,20,50],
            Children =
            [
                new BalancingTreeNode
                {
                    Values = [5,6]
                },
                new BalancingTreeNode
                {
                    Values = [8,10]
                },
                new BalancingTreeNode
                {
                    Values = [30,40]
                },
                new BalancingTreeNode
                {
                    Values = [60]
                }
            ]
        }, options => options
            .IgnoringCyclicReferences()
            .ExcludingMembersNamed("Parent"));
    }

    [Test]
    public void WhenMiddleChildSplits_ThenSiblingShouldBeInsertedImmediatelyAfterIt_OrderFour()
    {
        var service = new BalancingTreeService(4);

        foreach (var value in new[] { 10, 20, 30, 40, 50, 60, 35, 37 })
            service.Add(value);

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
        {
            Values = [20, 37, 50],
            Children =
            [
                new BalancingTreeNode { Values = [10] },
                new BalancingTreeNode { Values = [30, 35] },
                new BalancingTreeNode { Values = [40] },
                new BalancingTreeNode { Values = [60] }
            ]
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }

    [Test]
    public void WhenValuesAreAddedDescending_ThenLeftChildShouldSplitWithoutReorderingSiblings_OrderFour()
    {
        var service = new BalancingTreeService(4);

        foreach (var value in new[] { 60, 50, 40, 30, 20, 10, 0 })
            service.Add(value);

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
        {
            Values = [20, 40],
            Children =
            [
                new BalancingTreeNode { Values = [0, 10] },
                new BalancingTreeNode { Values = [30] },
                new BalancingTreeNode { Values = [50, 60] }
            ]
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }

    [Test]
    public void WhenInternalNodeSplits_ThenItsChildrenAndParentLinksShouldBePreserved_OrderFour()
    {
        var service = new BalancingTreeService(4);
        var insertedValues = Enumerable.Range(1, 40).ToArray();

        foreach (var value in insertedValues)
        {
            service.Add(value);

            if (value == 21)
            {
                service.BalancingTree.Root.Children.Count.Should().BeGreaterThan(2,
                    "the right internal node should have split into two siblings");
            }
        }

        ValidateNode(service.BalancingTree.Root, service.BalancingTree.Order, null, null);
        ReadInOrder(service.BalancingTree.Root).Should().Equal(insertedValues);
    }

    [TestCase(3, 173)]
    [TestCase(4, 947)]
    [TestCase(5, 2027)]
    public void WhenValuesAreInsertedInRandomOrder_ThenTreeShouldRemainValid(int order, int seed)
    {
        var service = new BalancingTreeService(order);
        var values = Enumerable.Range(1, 200).ToArray();
        var random = new Random(seed);
        random.Shuffle(values);

        foreach (var value in values)
            service.Add(value);

        ValidateNode(service.BalancingTree.Root, order, null, null);
        ReadInOrder(service.BalancingTree.Root).Should().Equal(Enumerable.Range(1, 200));
    }

    [Test]
    public void WhenDuplicateValuesAreInserted_ThenEveryOccurrenceShouldBeRetainedInSortedOrder()
    {
        var service = new BalancingTreeService(4);
        int[] values = [10, 5, 20, 10, 15, 5, 10, 20, 10, 15];

        foreach (var value in values)
            service.Add(value);

        ReadInOrder(service.BalancingTree.Root).Should().Equal(values.Order());
        ReadInOrder(service.BalancingTree.Root).Should().HaveCount(values.Length);
        ValidateNodeAllowingDuplicates(service.BalancingTree.Root, service.BalancingTree.Order, null, null);
    }

    [Test]
    public void WhenAllInsertedValuesAreDuplicates_ThenEveryOccurrenceShouldBeRetained()
    {
        var service = new BalancingTreeService(4);
        var values = Enumerable.Repeat(10, 40).ToArray();

        foreach (var value in values)
            service.Add(value);

        ReadInOrder(service.BalancingTree.Root).Should().Equal(values);
        ValidateNodeAllowingDuplicates(service.BalancingTree.Root, service.BalancingTree.Order, null, null);
    }

    [Test]
    public void WhenIAddOneAndTwo_ThenBalancingTreeShouldHaveOneNOdeWithTwoValues()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode
            {
                Values = [1, 2]
            }
        });
    }
    
    [Test]
    public void WhenIAddThree_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode 
            {
                Values = [2],
                Children = [new BalancingTreeNode() { Values = [1] }, new BalancingTreeNode() { Values = [3]}],
            }
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddFour_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));
        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode 
            {
                Values = [2],
                Children = [new BalancingTreeNode() { Values = [1] }, new BalancingTreeNode() { Values = [3,4]}],
            }
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddFive_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode 
            {
                Values = [2, 4],
                Children = [new BalancingTreeNode()
                {
                    Values = [1]
                }, 
                new BalancingTreeNode()
                {
                    Values = [3]
                }, 
                new BalancingTreeNode()
                {
                    Values = [5]
                }],
            }
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddSix_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        service.Add(6);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode 
            {
                Values = [2, 4],
                Children = [new BalancingTreeNode()
                    {
                        Values = [1]
                    }, 
                    new BalancingTreeNode()
                    {
                        Values = [3]
                    }, 
                    new BalancingTreeNode()
                    {
                        Values = [5, 6]
                    }],
            }
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddSeven_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        service.Add(6);
        service.Add(7);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        
        service.BalancingTree.Should().BeEquivalentTo(new BalancingTree
        {
            Order = 3, 
            Root = new BalancingTreeNode 
            {
                Values = [4],
                Children = [new BalancingTreeNode()
                    {
                        Values = [2],
                        Children = [
                            new BalancingTreeNode()
                            {
                                Values = [1]
                            },
                            new BalancingTreeNode()
                            {
                                Values = [3]
                            }
                        ]
                    }, 
                    new BalancingTreeNode()
                    {
                        Values = [6],
                        Children = [
                            new BalancingTreeNode()
                            {
                                Values = [5]
                            },
                            new BalancingTreeNode()
                            {
                                Values = [7]
                            }
                        ]
                    }
                ]
            }
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }

    [Test]
    public void WhenIAddEight_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        service.Add(6);
        service.Add(7);
        service.Add(8);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
            {
                Values = [4],
                Children =
                [
                    new BalancingTreeNode
                    {
                        Values = [2],
                        Children =
                        [
                            new BalancingTreeNode
                            {
                                Values = [1]
                            },
                            new BalancingTreeNode
                            {
                                Values = [3]
                            }
                        ]
                    },
                    new BalancingTreeNode
                    {
                        Values = [6],
                        Children = [
                            new BalancingTreeNode()
                            {
                                Values = [5]
                            },
                            new BalancingTreeNode()
                            {
                                Values = [7, 8]
                            }
                        ]
                    }
                ]
            }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddNine_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        service.Add(6);
        service.Add(7);
        service.Add(8);
        service.Add(9);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
        {
            Values = [4],
            Children =
            [
                new BalancingTreeNode
                {
                    Values = [2],
                    Children =
                    [
                        new BalancingTreeNode
                        {
                            Values = [1]
                        },
                        new BalancingTreeNode
                        {
                            Values = [3]
                        }
                    ]
                },
                new BalancingTreeNode
                {
                    Values = [6, 8],
                    Children = [
                        new BalancingTreeNode()
                        {
                            Values = [5]
                        },
                        new BalancingTreeNode()
                        {
                            Values = [7]
                        },
                        new BalancingTreeNode()
                        {
                            Values = [9]
                        }
                    ]
                }
            ]
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }
    
    [Test]
    public void WhenIAddTen_ThenBalancingTreeShouldHaveMNodes()
    {
        var service = new BalancingTreeService(3);
        
        service.Add(1);
        service.Add(2);
        service.Add(3);
        service.Add(4);
        service.Add(5);
        service.Add(6);
        service.Add(7);
        service.Add(8);
        service.Add(9);
        service.Add(10);
        
        TestContext.Out.Write(System.Text.Json.JsonSerializer.Serialize(service.BalancingTree, new JsonSerializerOptions( ) { ReferenceHandler = ReferenceHandler.IgnoreCycles, WriteIndented = true}));

        service.BalancingTree.Root.Should().BeEquivalentTo(new BalancingTreeNode
        {
            Values = [4],
            Children =
            [
                new BalancingTreeNode
                {
                    Values = [2],
                    Children =
                    [
                        new BalancingTreeNode
                        {
                            Values = [1]
                        },
                        new BalancingTreeNode
                        {
                            Values = [3]
                        }
                    ]
                },
                new BalancingTreeNode
                {
                    Values = [6, 8],
                    Children = [
                        new BalancingTreeNode()
                        {
                            Values = [5]
                        },
                        new BalancingTreeNode()
                        {
                            Values = [7]
                        },
                        new BalancingTreeNode()
                        {
                            Values = [9, 10]
                        }
                    ]
                }
            ]
        }, options => options.IgnoringCyclicReferences().ExcludingMembersNamed("Parent"));
    }

    private static void ValidateNode(BalancingTreeNode node, int order, int? minimum, int? maximum)
    {
        node.Values.Should().BeInAscendingOrder();
        node.Values.Count.Should().BeLessThan(order);
        node.Values.Should().OnlyContain(value =>
            (!minimum.HasValue || value > minimum.Value) &&
            (!maximum.HasValue || value < maximum.Value));

        if (node.Children.Count == 0)
            return;

        node.Children.Count.Should().Be(node.Values.Count + 1);

        for (var index = 0; index < node.Children.Count; index++)
        {
            var child = node.Children[index];
            child.Parent.Should().BeSameAs(node);

            var childMinimum = index == 0 ? minimum : node.Values[index - 1];
            var childMaximum = index == node.Values.Count ? maximum : node.Values[index];
            ValidateNode(child, order, childMinimum, childMaximum);
        }
    }

    private static void ValidateNodeAllowingDuplicates(
        BalancingTreeNode node,
        int order,
        int? minimum,
        int? maximum)
    {
        node.Values.Should().BeInAscendingOrder();
        node.Values.Count.Should().BeLessThan(order);
        node.Values.Should().OnlyContain(value =>
            (!minimum.HasValue || value >= minimum.Value) &&
            (!maximum.HasValue || value <= maximum.Value));

        if (node.Children.Count == 0)
            return;

        node.Children.Count.Should().Be(node.Values.Count + 1);

        for (var index = 0; index < node.Children.Count; index++)
        {
            var child = node.Children[index];
            child.Parent.Should().BeSameAs(node);

            var childMinimum = index == 0 ? minimum : node.Values[index - 1];
            var childMaximum = index == node.Values.Count ? maximum : node.Values[index];
            ValidateNodeAllowingDuplicates(child, order, childMinimum, childMaximum);
        }
    }

    private static IEnumerable<int> ReadInOrder(BalancingTreeNode node)
    {
        for (var index = 0; index < node.Values.Count; index++)
        {
            if (node.Children.Count > 0)
            {
                foreach (var value in ReadInOrder(node.Children[index]))
                    yield return value;
            }

            yield return node.Values[index];
        }

        if (node.Children.Count > 0)
        {
            foreach (var value in ReadInOrder(node.Children[^1]))
                yield return value;
        }
    }
}
