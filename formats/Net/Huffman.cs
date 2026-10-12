namespace Foley.Formats.Net;

/// <summary>
/// Huffman adaptativo de id Tech 3 con que se comprimen los mensajes de red y de demo.
/// Portado de ioquake3 code/qcommon/huffman.c (Huff_Init, Huff_addRef, increment, swap,
/// swaplist, Huff_offsetReceive, Huff_getBit) y code/qcommon/msg.c (MSG_initHuffman, msg_hData).
/// El árbol se construye insertando cada símbolo tantas veces como indica la tabla de
/// frecuencias; después queda fijo y se usa para decodificar.
/// </summary>
public sealed class Huffman
{
    public const int Nyt = 256;          // HMAX: "not yet transmitted"
    public const int InternalNode = 257; // HMAX + 1

    private sealed class Slot
    {
        public Node? Value;
    }

    private sealed class Node
    {
        public Node? Left, Right, Parent, Next, Prev;
        public Slot Head = null!; // nodo de mayor rango del bloque (node_t **head)
        public int Weight;
        public int Symbol;
    }

    private Node _tree;
    private readonly Node _lhead;
    private readonly Node?[] _loc = new Node?[Nyt + 1];

    /// <summary>
    /// Frecuencias de ioquake3 msg.c <c>msg_hData</c>.
    /// Que CoD1 use la misma tabla se comprueba decodificando sus demos (docs/formats/demo.md).
    /// </summary>
    public static readonly int[] Quake3Frequencies =
    {
        250315, 41193, 6292, 7106, 3730, 3750, 6110, 23283,
        33317, 6950, 7838, 9714, 9257, 17259, 3949, 1778,
        8288, 1604, 1590, 1663, 1100, 1213, 1238, 1134,
        1749, 1059, 1246, 1149, 1273, 4486, 2805, 3472,
        21819, 1159, 1670, 1066, 1043, 1012, 1053, 1070,
        1726, 888, 1180, 850, 960, 780, 1752, 3296,
        10630, 4514, 5881, 2685, 4650, 3837, 2093, 1867,
        2584, 1949, 1972, 940, 1134, 1788, 1670, 1206,
        5719, 6128, 7222, 6654, 3710, 3795, 1492, 1524,
        2215, 1140, 1355, 971, 2180, 1248, 1328, 1195,
        1770, 1078, 1264, 1266, 1168, 965, 1155, 1186,
        1347, 1228, 1529, 1600, 2617, 2048, 2546, 3275,
        2410, 3585, 2504, 2800, 2675, 6146, 3663, 2840,
        14253, 3164, 2221, 1687, 3208, 2739, 3512, 4796,
        4091, 3515, 5288, 4016, 7937, 6031, 5360, 3924,
        4892, 3743, 4566, 4807, 5852, 6400, 6225, 8291,
        23243, 7838, 7073, 8935, 5437, 4483, 3641, 5256,
        5312, 5328, 5370, 3492, 2458, 1694, 1821, 2121,
        1916, 1149, 1516, 1367, 1236, 1029, 1258, 1104,
        1245, 1006, 1149, 1025, 1241, 952, 1287, 997,
        1713, 1009, 1187, 879, 1099, 929, 1078, 951,
        1656, 930, 1153, 1030, 1262, 1062, 1214, 1060,
        1621, 930, 1106, 912, 1034, 892, 1158, 990,
        1175, 850, 1121, 903, 1087, 920, 1144, 1056,
        3462, 2240, 4397, 12136, 7758, 1345, 1307, 3278,
        1950, 886, 1023, 1112, 1077, 1042, 1061, 1071,
        1484, 1001, 1096, 915, 1052, 995, 1070, 876,
        1111, 851, 1059, 805, 1112, 923, 1103, 817,
        1899, 1872, 976, 841, 1127, 956, 1159, 950,
        7791, 954, 1289, 933, 1127, 3207, 1020, 927,
        1355, 768, 1040, 745, 952, 805, 1073, 740,
        1013, 805, 1008, 796, 996, 1057, 11457, 13504,
    };

    private static Huffman? _quake3;

    /// <summary>Árbol de decodificación de mensajes de id Tech 3 (se construye una vez).</summary>
    public static Huffman Quake3 => _quake3 ??= FromFrequencies(Quake3Frequencies);

    private Huffman()
    {
        // Huff_Init: el árbol y la lista empiezan con el nodo NYT.
        _tree = _lhead = new Node { Symbol = Nyt, Weight = 0 };
        _loc[Nyt] = _lhead;
    }

    /// <summary>MSG_initHuffman: inserta cada símbolo i, freq[i] veces, en orden.</summary>
    public static Huffman FromFrequencies(IReadOnlyList<int> frequencies)
    {
        if (frequencies.Count != 256) throw new ArgumentException("Se esperan 256 frecuencias.", nameof(frequencies));
        var h = new Huffman();
        for (var i = 0; i < 256; i++)
            for (var j = 0; j < frequencies[i]; j++)
                h.AddRef((byte)i);
        return h;
    }

    /// <summary>
    /// Huff_offsetReceive: decodifica un símbolo empezando en el bit <paramref name="bit"/>.
    /// Si se pasa de <paramref name="maxBit"/> devuelve 0 y deja bit = maxBit + 1, como el original.
    /// </summary>
    public int Receive(ReadOnlySpan<byte> data, ref int bit, int maxBit)
    {
        Node? node = _tree;
        while (node is not null && node.Symbol == InternalNode)
        {
            if (bit >= maxBit)
            {
                bit = maxBit + 1;
                return 0;
            }
            node = GetBit(data, ref bit) != 0 ? node.Right : node.Left;
        }
        return node?.Symbol ?? 0;
    }

    /// <summary>Huff_getBit: lee un bit sin comprimir (LSB primero).</summary>
    public static int GetBit(ReadOnlySpan<byte> data, ref int bit)
    {
        var t = (data[bit >> 3] >> (bit & 7)) & 1;
        bit++;
        return t;
    }

    // get_ppnode / free_ppnode: las ranuras liberadas se reciclan en orden LIFO, como en C.
    private readonly Stack<Slot> _freeSlots = new();

    private Slot NewSlot(Node? value)
    {
        var slot = _freeSlots.Count > 0 ? _freeSlots.Pop() : new Slot();
        slot.Value = value;
        return slot;
    }

    private void FreeSlot(Slot slot)
    {
        slot.Value = null;
        _freeSlots.Push(slot);
    }

    /// <summary>swap: intercambia la posición de dos nodos en el árbol.</summary>
    private void Swap(Node node1, Node node2)
    {
        var par1 = node1.Parent;
        var par2 = node2.Parent;

        if (par1 is not null)
        {
            if (par1.Left == node1) par1.Left = node2;
            else par1.Right = node2;
        }
        else _tree = node2;

        if (par2 is not null)
        {
            if (par2.Left == node2) par2.Left = node1;
            else par2.Right = node1;
        }
        else _tree = node1;

        node1.Parent = par2;
        node2.Parent = par1;
    }

    /// <summary>swaplist: intercambia dos nodos en la lista enlazada (actualiza rangos).</summary>
    private static void SwapList(Node node1, Node node2)
    {
        var par1 = node1.Next;
        node1.Next = node2.Next;
        node2.Next = par1;

        par1 = node1.Prev;
        node1.Prev = node2.Prev;
        node2.Prev = par1;

        if (node1.Next == node1) node1.Next = node2;
        if (node2.Next == node2) node2.Next = node1;
        if (node1.Next is not null) node1.Next.Prev = node1;
        if (node2.Next is not null) node2.Next.Prev = node2;
        if (node1.Prev is not null) node1.Prev.Next = node1;
        if (node2.Prev is not null) node2.Prev.Next = node2;
    }

    /// <summary>
    /// increment. En C es recursivo: tras actualizar el nodo llama a increment(parent) y al volver
    /// hace un ajuste final. Aquí es iterativo: los ajustes finales se aplican al terminar, del
    /// ancestro más alto al más bajo, que es el mismo orden en que la recursión los ejecuta.
    /// </summary>
    private void Increment(Node? node)
    {
        var pending = new Stack<Node>();
        while (node is not null)
        {
            if (node.Next is not null && node.Next.Weight == node.Weight)
            {
                var lnode = node.Head.Value!;
                if (lnode != node.Parent) Swap(lnode, node);
                SwapList(lnode, node);
            }
            if (node.Prev is not null && node.Prev.Weight == node.Weight)
                node.Head.Value = node.Prev;
            else
                FreeSlot(node.Head);
            node.Weight++;
            if (node.Next is not null && node.Next.Weight == node.Weight)
                node.Head = node.Next.Head;
            else
                node.Head = NewSlot(node);

            if (node.Parent is null) break;
            pending.Push(node);
            node = node.Parent;
        }

        while (pending.Count > 0)
        {
            var n = pending.Pop();
            if (n.Prev == n.Parent)
            {
                SwapList(n, n.Parent!);
                if (n.Head.Value == n) n.Head.Value = n.Parent;
            }
        }
    }

    /// <summary>Huff_addRef.</summary>
    private void AddRef(byte ch)
    {
        if (_loc[ch] is not null)
        {
            Increment(_loc[ch]);
            return;
        }

        // primera transmisión de este símbolo
        var tnode = new Node();
        var tnode2 = new Node { Symbol = InternalNode, Weight = 1, Next = _lhead.Next };
        if (_lhead.Next is not null)
        {
            _lhead.Next.Prev = tnode2;
            tnode2.Head = _lhead.Next.Weight == 1 ? _lhead.Next.Head : NewSlot(tnode2);
        }
        else
        {
            tnode2.Head = NewSlot(tnode2);
        }
        _lhead.Next = tnode2;
        tnode2.Prev = _lhead;

        tnode.Symbol = ch;
        tnode.Weight = 1;
        tnode.Next = _lhead.Next;
        if (_lhead.Next is not null)
        {
            _lhead.Next.Prev = tnode;
            // la rama "else" de C ("esto nunca debería pasar") apunta la ranura a tnode2
            tnode.Head = _lhead.Next.Weight == 1 ? _lhead.Next.Head : NewSlot(tnode2);
        }
        else
        {
            tnode.Head = NewSlot(tnode); // "esto nunca debería pasar"
        }
        _lhead.Next = tnode;
        tnode.Prev = _lhead;
        tnode.Left = tnode.Right = null;

        if (_lhead.Parent is not null)
        {
            // lhead es siempre el NYT
            if (_lhead.Parent.Left == _lhead) _lhead.Parent.Left = tnode2;
            else _lhead.Parent.Right = tnode2;
        }
        else
        {
            _tree = tnode2;
        }

        tnode2.Right = tnode;
        tnode2.Left = _lhead;
        tnode2.Parent = _lhead.Parent;
        _lhead.Parent = tnode.Parent = tnode2;

        _loc[ch] = tnode;
        Increment(tnode2.Parent);
    }
}
