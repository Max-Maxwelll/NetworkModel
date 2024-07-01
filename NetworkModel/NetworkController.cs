using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkModel.Pages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetworkModel
{
    [Route("api/[controller]/[action]")]
    [AllowAnonymous]
    [ApiController]
    public class NetworkController : ControllerBase
    {
        private readonly ILogger<NetworkController> logger;
        private readonly Random rnd = new Random();
        private string[] Colors = new string[] { "#97C2FC", "#9782FC", "#974DFC", "#9700FC", "#D000FC", "#FF00FC", "#FF004D" };

        public NetworkController(ILogger<NetworkController> logger)
        {
            this.logger = logger;
        }
        [HttpPost]
        public IActionResult Delete([FromBody]DeleteRequest request)
        {
            var time = DateTime.Now;
            var oldNodes = request.Nodes;
            request.Nodes = request.Nodes.Where(n => n.id != request.Node.id).Select((n, i) => { n.Index = i; return n; }).ToArray();
            //Array.Sort(request.Nodes, new NodeComparer());
            var newEdges = new List<Edge>();
            var countNodes = request.Nodes.Length;
            var groupsDistances = new Distance[countNodes][];
            var tree = new Dictionary<int, TreeNode>();

            for (int x = 0; x < request.Nodes.Length; x++)
            {
                var distances = new Distance[countNodes];
                for (int y = 0; y < request.Nodes.Length; y++)
                {
                    distances[y].Node = new DistanceNode { Id = request.Nodes[y].id, Index = request.Nodes[y].Index };
                    distances[y].Dis = request.Nodes[x].id ^ request.Nodes[y].id;
                }
                groupsDistances[x] = distances;
            }

            Action<Node[], int> func = (nodes, level) => { };
            func = (nodes, level) =>
            {
                var countNodes = nodes.Length;
                var masters = new List<Node>();

                Parallel.For(0, countNodes, (i) =>
                {
                    //request.Nodes[nodes[i].Index].color = "#97C2FC";
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
                            masters.Add(new Node { id = nodes[i].id, Index = nodes[i].Index});
                        }
                        lock (request.Nodes)
                        {
                            request.Nodes[nodes[i].Index].color = Colors[level];
                        }

                        return;
                    }

                    nodes[i].Master = nearFirst.Node.Id;
                    request.Nodes[nodes[i].Index].Master = nearFirst.Node.Id;

                    lock (tree)
                    {
                        if (tree.TryGetValue(nodes[i].id, out var treeNode))
                        {
                            treeNode.Master = nearFirst.Node.Id;
                            tree[nodes[i].id] = treeNode;
                        }
                        else
                        {
                            treeNode.Id = nodes[i].id;
                            treeNode.Master = nearFirst.Node.Id;
                            treeNode.Children = new List<int>();
                            tree.Add(nodes[i].id, treeNode);
                        }

                        if (tree.TryGetValue(nearFirst.Node.Id, out var treeMasterNode))
                        {
                            treeMasterNode.Children.Add(nodes[i].id);
                            tree[nearFirst.Node.Id] = treeMasterNode;
                        }
                        else
                        {
                            treeMasterNode.Id = nearFirst.Node.Id;
                            treeMasterNode.Children = new List<int>();
                            treeMasterNode.Children.Add(nodes[i].id);
                            tree.Add(nearFirst.Node.Id, treeMasterNode);
                        }
                    }

                    lock (newEdges)
                    {
                        newEdges.Add(new Edge { IndexFrom = nodes[i].Index, IndexTo = nearFirst.Node.Index, from = nodes[i].id, to = nearFirst.Node.Id, label = $">>> {request.Nodes[nearFirst.Node.Index].label}" });
                    }   
                });

                if (masters.Count > 1)
                    func?.Invoke(masters.ToArray(), ++level);
            };

            func(request.Nodes, 0);

            logger.LogInformation($"Time: {(DateTime.Now - time).TotalSeconds}");

            var deleted = request.Edges.Except(newEdges, new EdgeComparer())
                .Select(e => new Change { IsDeleted = true, Description = $"Link from {oldNodes[e.IndexFrom].label} to {oldNodes[e.IndexTo].label} deleted" });
            var added = newEdges.Except(request.Edges, new EdgeComparer())
                .Select(e => new Change { IsAdded = true, Description = $"Link from {request.Nodes[e.IndexFrom].label} to {request.Nodes[e.IndexTo].label} added" });

            return Ok(new DeleteResult
            {
                Changes = deleted.Union(added),
                Edges = newEdges,
                Nodes = request.Nodes,
                Tree = tree.Select(x => x.Value).ToArray()
            });
        }

        [HttpGet]
        public IActionResult GetTree(int count = 10)
        {
            var Nodes = new Node[count];
            var tree = new Dictionary<int, TreeNode>();

            for (int i = 0; i < Nodes.Length; i++)
            {
                Nodes[i].Index = i;
                Nodes[i].id = rnd.Next();
                Nodes[i].label = $"Node {i}";
                Nodes[i].title = Nodes[i].id.ToString();
            }

            var countNodes = Nodes.Length;
            var groupsDistances = new Distance[countNodes][];

            // Calculating distances
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
                        //lock (Nodes)
                        //{
                        //    Nodes[nodes[i].Index].color = Colors[1];
                        //}

                        return;
                    }

                    nodes[i].Master = nearFirst.Node.Id;
                    Nodes[nodes[i].Index].Master = nearFirst.Node.Id;

                    lock (tree)
                    {
                        if (tree.TryGetValue(nodes[i].id, out var treeNode))
                        {
                            treeNode.Master = nearFirst.Node.Id;
                            tree[nodes[i].id] = treeNode;
                        }
                        else
                        {
                            treeNode.Id = nodes[i].id;
                            treeNode.Master = nearFirst.Node.Id;
                            treeNode.Children = new List<int>();
                            tree.Add(nodes[i].id, treeNode);
                        }

                        if (tree.TryGetValue(nearFirst.Node.Id, out var treeMasterNode))
                        {
                            treeMasterNode.Children.Add(nodes[i].id);
                            tree[nearFirst.Node.Id] = treeMasterNode;
                        }
                        else
                        {
                            treeMasterNode.Id = nearFirst.Node.Id;
                            treeMasterNode.Children = new List<int>();
                            treeMasterNode.Children.Add(nodes[i].id);
                            tree.Add(nearFirst.Node.Id, treeMasterNode);
                        }
                    }

                    //lock (Edges)
                    //{
                    //    Edges.Add(new Edge { IndexFrom = nodes[i].Index, IndexTo = nearFirst.Node.Index, from = nodes[i].id, to = nearFirst.Node.Id, label = $"{Nodes[nodes[i].Index].label} -> {Nodes[nearFirst.Node.Index].label}" });
                    //}
                });

                if (masters.Count > 1)
                    func?.Invoke(masters.ToArray(), ++level);
            };

            func(Nodes, 0);

            return Ok(JsonConvert.SerializeObject(tree.Select(x => x.Value), Formatting.Indented));
        }

        public IActionResult GetDistances([FromBody] GetDistanceRequest request)
        {
            var countNodes = request.Nodes.Length;
            var distances = new Distance[countNodes];
            for (int y = 0; y < request.Nodes.Length; y++)
            {
                distances[y].Node = new DistanceNode { Id = request.Nodes[y].id, Index = request.Nodes[y].Index };
                distances[y].Dis = request.Node.id ^ request.Nodes[y].id;
            }

            return Ok(distances.OrderBy(d => d.Dis).ToArray());
        }

        [HttpPost]
        public IActionResult GetRegions([FromBody]RegionRequest request)
        {
            try
            {
                var tree = request.Tree.ToDictionary(x => x.Id);
                var innerRegion = new List<int>();
                var outerRegion = new List<int>();

                Action<int, Dictionary<int, TreeNode>> innerAction = (c, t) => { };
                Action<int, int, Dictionary<int, TreeNode>> outerAction = (e, c, t) => { };
                //Action<int, Dictionary<int, TreeNode>> nextOuterAction = (c, t) => { };
                Action<int, Dictionary<int, TreeNode>> childrenOuterAction = (c, t) => { };

                innerAction = (current, tree) =>
                {
                    innerRegion.Add(current);
                    foreach (var node in tree[current].Children)
                    {
                        innerAction?.Invoke(node, tree);
                    }
                };

                outerAction = (except, current, tree) =>
                {
                    if (current == 0) return;

                    lock (outerRegion)
                    {
                        outerRegion.Add(current);
                    }
                    outerAction?.Invoke(current, tree[current].Master, tree);

                    Parallel.ForEach(tree[current].Children, (node) =>
                    {
                        if (node != except) childrenOuterAction?.Invoke(node, tree);
                    });
                };

                childrenOuterAction = (current, tree) =>
                {

                    lock (outerRegion)
                    {
                        outerRegion.Add(current);
                    }

                    Parallel.ForEach(tree[current].Children, (node) =>
                    {
                        childrenOuterAction?.Invoke(node, tree);
                    });
                };

                innerAction(request.Current, tree);
                outerAction(request.Current, tree[request.Current].Master, tree);

                return Ok(new RegionResult { InnerRegion = innerRegion.ToArray(), OuterRegion = outerRegion.ToArray() });
            }
            catch(Exception ex)
            {
                return BadRequest(ex);
            }
        }

        //public IActionResult GetChain([FromBody] GetChainRequest request)
        //{
        //    var chain = new Dictionary<int, int>();
        //    foreach(var edge in request.Edges)
        //    {
        //        chain.Add(edge.IndexFrom, edge.IndexTo);
        //    }
        //    return
        //}
    }
    public class RegionRequest
    {
        public int Current { get; set; }
        public TreeNode[] Tree { get; set; }
    }
    public class RegionResult
    {
        public int[] InnerRegion { get; set; }
        public int[] OuterRegion { get; set; }
    }
    public struct TreeNode
    {
        public int Id { get; set; }
        public int Master { get; set; }
        public List<int> Children { get; set; }
    }
    public class GetChainRequest
    {
        public Node[] Nodes { get; set; }
        public Edge[] Edges { get; set; }
    }
    public class GetDistanceRequest
    {
        public Node Node { get; set; }
        public Node[] Nodes { get; set; }
    }
    public class DeleteRequest
    {
        public Node Node { get; set; }
        public Node[] Nodes { get; set; }
        public List<Edge> Edges { get; set; }
    }
    public class DeleteResult
    {
        public IEnumerable<Change> Changes { get; set; }
        public IEnumerable<Node> Nodes { get; set; }
        public IEnumerable<Edge> Edges { get; set; }
        public IEnumerable<TreeNode> Tree { get; set; }
    }

    public struct Change
    {
        public bool IsAdded { get; set; }
        public bool IsDeleted { get; set; }
        public string Description { get; set; }
    }
    class EdgeComparer : IEqualityComparer<Edge>
    {
        public bool Equals(Edge x, Edge y)
        {

            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                return false;

            return x.from == y.from && x.to == y.to;
        }

        public int GetHashCode(Edge edge)
        {
            if (ReferenceEquals(edge, null)) return 0;

            int hashEdgeFrom = edge.from.GetHashCode();
            int hashEdgeTo = edge.to.GetHashCode();

            return hashEdgeFrom ^ hashEdgeTo;
        }
    }
}
