using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NetworkModel.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly Random rnd = new Random();
        private const int Count = 30;
        public double Performance { get; set; } = 0;
        public Node[] Nodes;
        public List<Edge> Edges = new List<Edge>();
        public Dictionary<int, TreeNode> Tree = new Dictionary<int, TreeNode>();
        private string[] Colors = new string[]{ "#97C27D", "#97A300", "#975900", "#970000", "#DB0000", "#DB00A0", "#DB00FF", "#700094" };

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet(int count = 100)
        {
            Nodes = new Node[count];
            
            for (int i = 0; i < Nodes.Length; i++)
            {
                Nodes[i].Index = i;
                Nodes[i].id = rnd.Next();
                Nodes[i].label = $"Node {i}";
                Nodes[i].title = Nodes[i].id.ToString();
            }

            var time = DateTime.Now;

            var countNodes = Nodes.Length;
            var groupsDistances = new Distance[countNodes][];

            for (int x = 0; x < Nodes.Length; x++)
            {
                var distances = new Distance[countNodes];
                for (int y = 0; y < Nodes.Length; y++)
                {
                    distances[y].Node = new DistanceNode { Id = Nodes[y].id, Index = Nodes[y].Index };
                    distances[y].Dis = Nodes[x].id ^ Nodes[y].id;
                }
                groupsDistances[x] = distances;
            }

            Action<Node[], int> func = (n, l) => { };
            func = (nodes, level) =>
            {
                var countNodes = nodes.Length;
                var masters = new List<Node>();

                Parallel.For(0, countNodes, (i) =>
                {
                    //Nodes[nodes[i].Index].color = "#97C2FC";
                    // for current
                    var dists = nodes.Select(n => groupsDistances[nodes[i].Index][n.Index]).ToArray();
                    Array.Sort(dists, new DistanceComparer());
                    var nearFirst = dists.Skip(1).FirstOrDefault();
                    // for nearest
                    var secondDists = nodes.Select(n => groupsDistances[nearFirst.Node.Index][n.Index]).ToArray();
                    Array.Sort(secondDists, new DistanceComparer());
                    var nearSecond = secondDists.Skip(1).FirstOrDefault();

                    if (nodes[i].id < nearFirst.Node.Id && nearSecond.Node.Id == nodes[i].id)
                    {
                        lock (masters)
                        {
                            masters.Add(new Node { id = nodes[i].id, Index = nodes[i].Index });
                        }
                        lock (Nodes)
                        {
                            Nodes[nodes[i].Index].color = Colors[level];
                        }
                        
                        return;
                    }

                    nodes[i].Master = nearFirst.Node.Id;
                    Nodes[nodes[i].Index].Master = nearFirst.Node.Id;

                    lock (Tree)
                    {
                        if (Tree.TryGetValue(nodes[i].id, out var treeNode))
                        {
                            treeNode.Master = nearFirst.Node.Id;
                            Tree[nodes[i].id] = treeNode;
                        }
                        else
                        {
                            treeNode.Id = nodes[i].id;
                            treeNode.Master = nearFirst.Node.Id;
                            treeNode.Children = new List<int>();
                            Tree.Add(nodes[i].id, treeNode);
                        }

                        if (Tree.TryGetValue(nearFirst.Node.Id, out var treeMasterNode))
                        {
                            treeMasterNode.Children.Add(nodes[i].id);
                            Tree[nearFirst.Node.Id] = treeMasterNode;
                        }
                        else
                        {
                            treeMasterNode.Id = nearFirst.Node.Id;
                            treeMasterNode.Children = new List<int>();
                            treeMasterNode.Children.Add(nodes[i].id);
                            Tree.Add(nearFirst.Node.Id, treeMasterNode);
                        }
                    }

                    lock (Edges)
                    {
                        Edges.Add(new Edge { IndexFrom = nodes[i].Index, IndexTo = nearFirst.Node.Index, from = nodes[i].id, to = nearFirst.Node.Id, label = $"{Nodes[nodes[i].Index].label} -> {Nodes[nearFirst.Node.Index].label}" });
                    }
                });

                if (masters.Count > 1)
                    func?.Invoke(masters.ToArray(), ++level);
            };

            func(Nodes, 0);

            var performance = (DateTime.Now - time).TotalSeconds;
            _logger.LogInformation($"Time: {performance}");
            Performance = performance;
            //_logger.LogInformation($"Time master: {masterSeconds}");
            //var edges = Edges.Select(e => e.from);
            //int countSingleNodes = 0;
            //foreach (var node in Nodes)
            //{
            //    if (!edges.Contains(node.id))
            //    {
            //        countSingleNodes++;
            //        _logger.LogInformation($"{node.id} is single");
            //    }
            //}
            //_logger.LogInformation($"Count single nodes: {countSingleNodes}, Master nodes: {Masters.Count}");
        }
    }
    public struct Node
    {
        [JsonIgnore]
        public int Index { get; set; }
        public int id { get; set; }
        [JsonIgnore]
        public int Master { get; set; }
        public string title { get; set; }
        public string label { get; set; }
        public string color { get; set; }
    }
    public struct Distance
    {
        public DistanceNode Node { get; set; }
        public int Dis { get; set; }
    }
    public struct DistanceNode
    {
        public int Id { get; set; }
        public int Index { get; set; }
    }
    public struct Edge
    {
        public int IndexFrom { get; set; }
        public int IndexTo { get; set; }
        public int from { get; set; }
        public int to { get; set; }
        public string label { get; set; }
    }

    public class DistanceComparer : IComparer<Distance>
    {
        int IComparer<Distance>.Compare([AllowNull] Distance x, [AllowNull] Distance y)
        {
            if (x.Dis > y.Dis) return 1;
            else if (x.Dis < y.Dis) return -1;
            else return 0;
        }
    }

    public class NodeComparer : IComparer<Node>
    {
        int IComparer<Node>.Compare([AllowNull] Node x, [AllowNull] Node y)
        {
            if (x.id > y.id) return 1;
            else if (x.id < y.id) return -1;
            else return 0;
        }
    }
}
