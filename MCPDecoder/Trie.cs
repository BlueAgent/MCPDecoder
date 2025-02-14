using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MCPDecoder
{
    /// <summary>
    /// https://en.wikipedia.org/wiki/Trie
    /// </summary>
    internal class Trie
    {
        private readonly Node root;

        public Trie()
        {
            root = new Node();
        }

        public bool Add(string value)
        {
            var queue = new Queue<char>(value);
            var node = root;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                Node child;
                if (!node.Children.TryGetValue(c, out child))
                {
                    child = new Node()
                    {
                        Value = c,
                    };
                    node.Children.Add(c, child);
                }
                node = child;
            }

            if (node.IsTerminal)
            {
                return false;

            }
            else
            {
                node.IsTerminal = true;
                return true;
            }
        }

        public bool Remove(string value)
        {
            var queue = new Queue<char>(value);
            var stack = new Stack<Node>();
            var node = root;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                stack.Push(node);
                if (!node.Children.TryGetValue(c, out node))
                {
                    return false;
                }
            }

            if (node.IsTerminal)
            {
                node.IsTerminal = false;

                while (stack.Count > 1)
                {
                    var current = stack.Pop();
                    if (current.IsTerminal || current.Children.Count > 0)
                    {
                        break;
                    }
                    stack.Peek().Children.Remove(current.Value);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return ToRegex();
        }

        public string ToRegex()
        {
            var builder = new StringBuilder();

            builder.Append("(?:");

            ToRegexInternal(builder, root);

            builder.Append(')');

            return builder.ToString();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Debugging")]
        private string ToDebugTree()
        {
            var first = true;
            var builder = new StringBuilder();
            var stack = new Stack<(int depth, Node node)>();

            stack.Push((-1, root));

            while (stack.Count > 0)
            {
                var (depth, node) = stack.Pop();

                if (depth >= 0)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.AppendLine();
                    }

                    builder.Append(' ', depth);
                    builder.Append(node.Value);

                    while (node.Children.Count == 1)
                    {
                        node = node.Children.Values.Single();
                        builder.Append(node.Value);
                    }
                }

                var childDepth = depth + 1;

                foreach (var childNode in node.Children.Values.OrderBy(n => n.Value))
                {
                    stack.Push((childDepth, childNode));
                }
            }

            return builder.ToString();
        }

        private void ToRegexInternal(StringBuilder builder, Node node)
        {
            var spanBuilder = new StringBuilder();
            if (node.Value != '\0')
            {
                spanBuilder.Append(node.Value);
            }

            while (!node.IsTerminal &&node.Children.Count == 1)
            {
                node = node.Children.Values.Single();
                spanBuilder.Append(node.Value);
            }

            builder.Append(Regex.Escape(spanBuilder.ToString()));

            if (node.Children.Count > 0)
            {
                builder.Append("(?:");

                var first = true;

                foreach (var childNode in node.Children.Values.OrderBy(n => n.Value))
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.Append('|');
                    }

                    ToRegexInternal(builder, childNode);
                }

                builder.Append(')');

                if (node.IsTerminal)
                {
                    builder.Append('?');
                }
            }
        }

        private class Node
        {
            public IDictionary<char, Node> Children { get; } = new Dictionary<char, Node>();
            public bool IsTerminal { get; set; }
            public char Value { get; set; }
        }
    }
}
