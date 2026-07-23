namespace sql_storage_engine;

public class BalancingTree
{
    public BalancingTreeNode Root { get; set; } = new();
    public int Order { get; set; }
}

public class BalancingTreeNode
{
    public BalancingTreeNode? Parent { get; set; }
    public List<int> Values { get; set; } = [];
    public List<BalancingTreeNode> Children { get; set; } = [];
}

public class BalancingTreeService(int order)
{
    private int maxChildren = order;
    private int maxValues = order - 1;
    
    public BalancingTree BalancingTree { get; set; } = new() { Order = order, Root = new BalancingTreeNode() };

    public void Add(int value)
        => Add(BalancingTree.Root, value);
    
    public void Add(BalancingTreeNode node, int value, bool ignoreCHildren = false)
    {
        //When we split, these both should exist
        if (node.Children.Any() && !ignoreCHildren)
        {
            if (AddValueToChildNode(node, value))
            {
                return;
            }
        }

        AddToValues(node, value);

        if (node.Values.Count() <= maxValues && node.Children.Count() <= maxChildren) return;
        
        SplitIntoMChildren(node);
    }

    private void AddToValues(BalancingTreeNode node, int value)
    {
        for (int i = 0; i < node.Values.Count(); i++)
        {
            if (value < node.Values.ElementAt(i))
            {
                node.Values.Insert(i, value);
                return;
            }
        }
        
        node.Values.Add(value);
        return;
    }

    private bool AddValueToChildNode(BalancingTreeNode node, int value)
    {
        for (int i = 0; i < node.Values.Count(); i++)
        {
            if (value < node.Values[i])
            {
                Add(node.Children.ElementAt(i), value);
                return true;
            }
        }
        
        Add(node.Children.Last(), value);

        return true;
    }

    private void SplitIntoMChildren(BalancingTreeNode node)
    {
        int midpoint = node.Parent == null && !node.Children.Any()
            ? (node.Values.Count() - 1) / 2
            : node.Values.Count() / 2;
        int midValue = node.Values.ElementAt(midpoint);
        
        if (node.Parent != null)
        {
            var parent = node.Parent;
            var nodeIndex = parent.Children.IndexOf(node);
            var leftNodeValues = node.Values.Take(midpoint).ToList();
            var rightNodeValues = node.Values.Skip(midpoint + 1).ToList();
            var oldChildren = node.Children;
            
            node.Values = leftNodeValues;
            
            var newParentRightNode = new BalancingTreeNode { Parent = parent, Values = rightNodeValues };

            if (oldChildren.Any())
            {
                node.Children = oldChildren.Take(midpoint + 1).ToList();
                newParentRightNode.Children = oldChildren.Skip(midpoint + 1).ToList();

                foreach (var child in newParentRightNode.Children)
                    child.Parent = newParentRightNode;
            }
            
            parent.Children.Insert(nodeIndex + 1, newParentRightNode);
            
            Add(parent, midValue, true);

            return;
        }
        
        var leftNode = new BalancingTreeNode { Parent = node, Values = node.Values.Take(midpoint).ToList() };
        var rightNode = new BalancingTreeNode { Parent = node, Values = node.Values.Skip(midpoint + 1).ToList() };

        if (node.Children.Any())
        {
            leftNode.Children = node.Children.Take(midpoint + 1).ToList();
            
            foreach(var entry in leftNode.Children)
                entry.Parent = leftNode;
            
            rightNode.Children = node.Children.Skip(midpoint + 1).ToList();
            
            foreach(var entry in rightNode.Children)
                entry.Parent = rightNode;
        }

        node.Values = [node.Values.ElementAt(midpoint)];
        
        node.Children = [leftNode, rightNode];
    }
}
