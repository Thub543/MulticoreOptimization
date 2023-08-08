using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Repräsentiert einen Graphen und bestimmt seine Eigenschaften
/// Wird über Graph.FromFile(filename) erstellt.
/// </summary>
public class Graph
{
    private readonly int[,] _adjacency;
    private readonly int?[,] _distanceMatrix;

    // Die Anzahl der Knoten des eingelesenen Graphen.
    public int NodeCount { get; }

    // Die Anzahl der Kanten des Graphen.
    public int EdgeCount
    {
        get
        {
            int count = 0;
            // Der Graph ist ungerichtet, wir zählen daher nur den unteren Bereich der Adjazenzmatrix.
            for (int row = 0; row < NodeCount; row++)
                for (int col = 0; col <= row; col++)
                    if (_adjacency[row, col] > 0) count++;
            return count;
        }
    }

    // Der kürzeste Abstand zwischen 2 Knoten. Wird im Konstruktor vorberechnet.
    // Dies ist die wichtigste Datenstruktur, auf deren Basis alle Metriken berchnet werden.
    // Es wird int? verwendet, für unendliche Distanzen wird null gespeichert.
    public int?[,] DistanceMatrix => _distanceMatrix;

    // Liefert eine Zahlenliste aller Knoten, von 0 beginnend (0, 1, 2, ..., NoteCount-1)
    // Das brauchen wir, um einfach durch die Knoten mit einer Schleife iterieren zu können.
    public IEnumerable<int> Nodes => Enumerable.Range(0, NodeCount);

    // Ein Graph ist zusammenhängend, wenn ich von einem Knoten alle anderen erreichen kann.
    // --> Die Distanz ist immer kleiner unendlich, also ungleich null.
    // Wir nehmen den ersten Knoten ohne Beschränkung der Allgemeinheit, denn in einem
    // zusammenhängenden Graphen ist auch jeder Knoten mit dem ersten Knoten verbunden.
    public bool IsConnected => !Nodes.Any(node => _distanceMatrix[0, node] == null);

    // Der Durchmesser ist unendlich, wenn der Graph nicht zusammenhängend ist.
    // Ansonsten nehmen wir die maximale Exzentrizität aller Knoten im Graphen.
    public int? Diameter => !IsConnected ? null : Nodes.Select(node => GetEccentricity(node)).Max();

    // Der Radius ist unendlich, wenn der Graph nicht zusammenhängend ist.
    // Ansonsten nehmen wir die minimale Exzentrizität aller Knoten im Graphen.
    public int? Radius => !IsConnected ? null : Nodes.Select(node => GetEccentricity(node)).Min();

    // Das Zentrum enthält alle Knoten, dessen Exzentrizität gleich dem Radius ist.
    // http://www.informatik.uni-trier.de/~naeher/Professur/PROJECTS/vogt/Thema.html
    public int[] Center
    {
        get
        {
            var radius = Radius;
            if (radius is null) return Array.Empty<int>();
            return Nodes.Where(node => GetEccentricity(node) == radius).ToArray();
        }
    }

    /// <summary>
    /// Konstruktor. Initialisiert die Adjazenzmatrix und berechnet die Distanzmatrix.
    /// </summary>
    private Graph(int[,] adjacency)
    {
        var nodeCount = adjacency.GetLength(0);
        // Wir prüfen, ob der Graph auch wirklich ungerichtet ist. Dafür prüfen wir den unteren
        // Teil der Adjazenzmatrix und vergleichen ihn mit dem oberen Wert.
        for (int row = 0; row < nodeCount; row++)
            for (int col = 0; col <= row; col++)
                if (adjacency[row, col] != adjacency[row, col])
                    throw new GraphException($"Die Kante {row} zu {col} ist nicht ungerichtet");

        _adjacency = adjacency;
        NodeCount = nodeCount;
        _distanceMatrix = new int?[NodeCount, NodeCount];
        CalcDistanceMatrix();
    }

    private void EnsureValidNode(int node)
    {
        if (node < 0 || node >= NodeCount)
            throw new GraphException($"Invalid Node {node}");
    }

    /// <summary>
    /// Liest eine CSV Datei mit der Adjazenzmatrix ein. Sie hat den Aufbau
    /// 1;0;1;0;0...
    /// (0 = nicht verbunden, 1 = verbunden)
    /// Das Trennzeichen ist beliebig, es werden alle Zahlen in der Zeile gesucht.
    /// </summary>
    public static Graph FromFile(string filename)
    {
        var rowSplitRegex = new Regex(@"\d+", RegexOptions.Compiled);
        // Leere Zeilen werden ignoriert. In Datenzeilen werden alle Zahlen gelesen.
        // Somit funktioniert das Lesen mit jedem Trennzeichen.
        var lines = File
            .ReadAllLines(filename, System.Text.Encoding.UTF8)
            .Where(line => rowSplitRegex.IsMatch(line))
            .Select(line => rowSplitRegex.Matches(line).Select(m => int.Parse(m.Value)).ToList())
            .ToList();
        int nodeCount = lines.Count;
        var adjacency = new int[nodeCount, nodeCount];
        int row = 0;
        foreach (var line in lines)
        {
            if (line.Count != nodeCount)
                throw new GraphException($"Zeile {row + 1} hat eine ungültige Anzahl an Werten. Erwartet: {nodeCount}.");
            int col = 0;
            foreach (var value in line)
                adjacency[row, col++] = value;
            row++;
        }
        return new Graph(adjacency);
    }

    /// <summary>
    /// Berechnet die Distanzmatrix für jeden Knoten im Graphen.
    /// https://en.wikipedia.org/wiki/Floyd%E2%80%93Warshall_algorithm
    /// </summary>
    private void CalcDistanceMatrix()
    {
        // Zuerst wird die Distanzmatrix initialisiert.
        // Jeder Knoten hat zu sich selbst den Abstand 0.
        // Ist ein Knoten direkt mit einem anderen Knoten verbunden, so nehmen wir den Wert aus
        // der Adjazenzmatrix.
        for (int row = 0; row < NodeCount; row++)
            for (int col = 0; col < NodeCount; col++)
            {
                if (row == col) { _distanceMatrix[row, col] = 0; continue; }
                var weight = _adjacency[row, col];
                // Damit wir nicht 0 für nicht verbundene Knoten als Distanzwert schreiben, prüfen
                // wir auf > 0.
                if (weight > 0) { _distanceMatrix[row, col] = weight; continue; }
            }

        // Idee: Wir halten einen Knoten fest (k).
        // Dann nehmen wir ein Knotenpaar aus dem Graphen (i und j).
        // Wir gehen von i über k nach j (i -> k -> j) und messen den Abstand. Ist der neue Abstand
        // kleiner als der gespeicherte, schreiben wir ihn in die Distanzmatrix. So finden wir den
        // kleinsten Abstand zwischen 2 Knoten. Der k Knoten ist notwendig, da die Knoten ja nicht
        // direkt verbunden sind. Diese haben wir schon im vorigen Schritt initialisiert.
        for (int k = 0; k < NodeCount; k++)
            for (int i = 0; i < NodeCount; i++)
                for (int j = 0; j < NodeCount; j++)
                {
                    var dist = _distanceMatrix[i, k] + _distanceMatrix[k, j];
                    if (_distanceMatrix[i, j] is null || _distanceMatrix[i, j] > dist)
                        _distanceMatrix[i, j] = dist;
                }
    }

    /// <summary>
    /// Gibt den Grad eines Knotens zurück.
    /// </summary>
    public int GetDegree(int node) => Nodes.Count(n => _adjacency[node, n] > 0);

    /// <summary>
    /// Liefert eine Liste aller Knoten, die vom angegebenen Knoten aus erreicht werden können.
    /// Erreicht bedeutet, dass auch mehrere Knoten dazwischen liegen können, deswegen verwenden wir
    /// die Distanzmatrix und nicht die Adjazenzmatrix.
    /// </summary>
    private IEnumerable<int> GetReachableNodes(int startNode) =>
        Nodes.Where(node => _distanceMatrix[startNode, node] != null);

    /// <summary>
    /// Liefert alle Teilgraphen als Liste von Knotenarrays.
    /// Dabei gehen wir vom ersten Knoten im Graphen aus und prüfen über die Distanzmatrix,
    /// welche Knoten erreichbar (auch über mehrere Stufen) sind.
    /// Diese erreichbaren Knoten werden dann aus der Liste der nicht besuchten Knoten entfernt,
    /// und wir prüfen erneut mit dem ersten nicht besuchten Knoten die Distanzmatrix.
    /// yied return liefert einen Enumerator. Man könnte auch eine Liste anlegen,
    /// das Array des Teilgraphen hinzufügen und diese Liste dann zurückgeben.
    /// </summary>
    public IEnumerable<int[]> GetSubgraphs()
    {
        // Eine Zahlenfolge 0, ..., n-1 generieren, die alle vorhandenen Knoten auflistet.
        var unvisited = Nodes.ToImmutableList();
        while (unvisited.Count != 0)
        {
            // Beim ersten noch nicht besuchten Knoten starten.
            var startNode = unvisited[0];
            // Wohin gibt es Wege?
            var reachableNodes = GetReachableNodes(startNode).ToArray();
            // Alle erreichbaren Knoten aus der Liste der noch nicht besuchten Knoten entfernen.
            unvisited = unvisited.RemoveRange(reachableNodes);
            yield return reachableNodes;
        }
    }

    /// <summary>
    /// Löscht einen Knoten aus der Adjazenzmatrix. Der neue Graph wird zurückgegeben. d. h. der
    /// bestehende Graph wird nicht verändert. Der neue Graph hat natürlich 1 Knoten weniger.
    /// Das ist für die Ermittlung der Artikulationspunkte wichtig.
    /// </summary>
    public Graph RemoveNode(int node)
    {
        EnsureValidNode(node);
        var adjacency = new int[NodeCount - 1, NodeCount - 1];

        // Die Adjazenzmatrix wird bis auf den zu entfernenden Knoten kopiert.
        for (int row = 0, destRow = 0; row < NodeCount; row++)
        {
            if (row == node) continue;
            for (int col = 0, destCol = 0; col < NodeCount; col++)
            {
                if (col == node) continue;
                adjacency[destRow, destCol++] = _adjacency[row, col];
            }
            destRow++;
        }
        return new Graph(adjacency);
    }

    /// <summary>
    /// Löscht eine Kante aus dem Graphen und gibt den neuen Graphen zurück.
    /// Der aktuelle Graph wird nicht verändert.
    /// </summary>
    public Graph RemoveEdge(int node1, int node2)
    {
        EnsureValidNode(node1);
        EnsureValidNode(node2);

        var adjacency = new int[NodeCount, NodeCount];
        Array.Copy(_adjacency, adjacency, _adjacency.Length);

        adjacency[node1, node2] = 0;
        adjacency[node2, node1] = 0;
        return new Graph(adjacency);
    }

    
    public IEnumerable<int> GetArticulationsSync()
    {
        var subgraphCount = GetSubgraphs().Count();
        // Geht die Knoten 0, 1, ..., NodeCount-1 durch.
        foreach (var node in Nodes){
            var newGraph = RemoveNode(node);
            if (newGraph.GetSubgraphs().Count() > subgraphCount)
                yield return node;
        }
    }
    
    /// <summary>
    /// Liefert eine Liste der Artikulationspunkte. Dabei entfernen wir den zu prüfenden Knoten und
    /// prüfen, ob der Graph in mehrere Teilgraphen als vorher zerfällt.
    /// https://mathworld.wolfram.com/ArticulationVertex.html
    /// </summary>
    public async Task<List<int>> GetArticulationsAsync() {
        var subgraphCount = GetSubgraphs().Count();
        var tasks = new List<Task<int>>();  
        foreach (var node in Nodes) {
            var task = Task.Run(() => {
                var newGraph = RemoveNode(node);
                if (newGraph.GetSubgraphs().Count() > subgraphCount) 
                    return node;
                return -1;
            });
            tasks.Add(task);
        }
        await Task.WhenAll(tasks);
        return tasks.Select(s => s.Result).Where(n => n != -1).ToList();
    }

    /// <summary>
    /// Die Exzentrizität ist die Distanz zum am Weitesten entfernten Knoten.
    /// Da wir schon die Distanzmatrix haben, ist die Berechnung einfach.
    /// In einem nicht zusammenhängenden Graphen ist die Exzentrizität von jedem Knoten unendlich.
    /// https://mathworld.wolfram.com/GraphEccentricity.html
    /// </summary>
    public int? GetEccentricity(int startNode)
    {
        if (!IsConnected) { return null; }
        return Nodes
             .Select(node => _distanceMatrix[startNode, node])
             .Max();
    }

    /// <summary>
    /// Liefert eine Liste von Kanten, die - wenn man sie entfernt - den Graphen in mehrere
    /// Teilgraphen aufteilen würden. Das sind dann Brücken.
    /// Da der Graph ungerichtet ist, müssen wir nur den Teil unter der Diagonale
    /// der Adjazenzmatrix analysieren.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<int[]> GetEdgeSeparators()
    {
        var subgraphCount = GetSubgraphs().Count();
        for (int node1 = 0; node1 < NodeCount; node1++)
            for (int node2 = 0; node2 <= node1; node2++)
                if (_adjacency[node1, node2] > 0)
                {
                    var newGraph = RemoveEdge(node1, node2);
                    if (newGraph.GetSubgraphs().Count() > subgraphCount)
                        yield return new int[] { node2, node1 };
                }
    }
    
    
    public async Task<List<int[]>> GetEdgeSeparatorsParallel() {
        var subgraphCount = GetSubgraphs().Count();
        List<int[]> result = new();
        var tasks = new Task[NodeCount];
        for (var node1 = 0; node1 < NodeCount; node1++) {
            var n = node1;
            tasks[node1] = Task.Run(() => {
                for (var node2 = 0; node2 <= n; node2++) {
                    if (_adjacency[n, node2] <= 0) continue;
                    var newGraph = RemoveEdge(n, node2);
                    if (newGraph.GetSubgraphs().Count() <= subgraphCount) continue;
                    lock (result) {
                        result.Add(new int[] { node2, n });
                    }
                }
            });
        }
        await Task.WhenAll(tasks);
        return result;
    }
}