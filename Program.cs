using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

internal class Program
{
    private static void Main(string[] args)
    {
        // *****************************************************************************************
        // Datei auswählen
        // *****************************************************************************************
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var filenames = currentDir.GetFiles("*.csv")
            .Concat(currentDir.GetFiles("*.txt"))
            .Concat(currentDir.GetFiles("*.tsv"))
            .OrderBy(f => f.Name).ToList();
        var files = string.Join(", ", filenames.Select((f, idx) => $"[{idx}] {f.Name}"));

        PrintInColor($"GEFUNDENE DATEIEN IN {currentDir.FullName}:", ConsoleColor.Blue);
        Console.WriteLine(files);
        int filenr = 0;
        do
            Console.Write("Welche Datei soll gelesen werden (CTRL+C für Abbruch)? ");
        while (!int.TryParse(Console.ReadLine(), out filenr) || filenr < 0 || filenr >= filenames.Count);
        var filename = filenames[filenr].FullName;

        // *****************************************************************************************
        // Graphen aufbauen und analysieren
        // *****************************************************************************************
        var sw = new Stopwatch();
        sw.Start();
        var graph = Graph.FromFile(filename);
        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"Graph in {sw.ElapsedMilliseconds} ms analysiert.");
        Console.WriteLine();
        PrintInColor("ANALYSE DES GRAPHEN:", ConsoleColor.Blue);

        if (graph.NodeCount < 100)
        {
            Console.WriteLine($"Distanzmatrix des Graphen (leer = unendlich):");
            PrintDistanceMatrix(graph.DistanceMatrix);
        }

        Console.WriteLine($"Anzahl der Knoten: {graph.NodeCount}");
        Console.WriteLine($"Anzahl der Kanten: {graph.EdgeCount}");

        Console.WriteLine($"Knotengrade:");
        var degrees = graph.Nodes.Take(100).Select(node => $"[{node}]: {graph.GetDegree(node)}");
        Console.WriteLine(string.Join(", ", degrees));

        var subgraphs = graph.GetSubgraphs().ToList();
        Console.WriteLine($"Der Graph hat {subgraphs.Count} Komponenten:");
        foreach (var subgraph in subgraphs)
            Console.WriteLine(JsonSerializer.Serialize(subgraph));

        Console.WriteLine($"Exzentrizitäten der Knoten:");
        var eccentricities = graph.Nodes.Take(100).Select(node => $"[{node}]: {(graph.GetEccentricity(node)?.ToString() ?? "inf")}");
        Console.WriteLine(string.Join(", ", eccentricities));

        Console.WriteLine($"Durchmesser des Graphen: {graph.Diameter?.ToString() ?? "inf"}");
        Console.WriteLine($"Radius des Graphen:      {graph.Radius?.ToString() ?? "inf"}");

        Console.Write($"Zentrum des Graphen:             ");
        Console.WriteLine(JsonSerializer.Serialize(graph.Center));

        sw.Restart();
        var articulations = graph.GetArticulations().ToList();
        sw.Stop();
        Console.Write($"Artikulationspunkte des Graphen ({sw.ElapsedMilliseconds} ms): ");
        Console.WriteLine(JsonSerializer.Serialize(articulations));

        sw.Restart();
        var separators = graph.GetEdgeSeparators().ToList();
        sw.Stop();
        Console.Write($"Brücken des Graphen: ({sw.ElapsedMilliseconds} ms): ");
        Console.WriteLine(JsonSerializer.Serialize(separators));

        //sw.Restart();
        //separators = await graph.GetEdgeSeparatorsParallel();
        //sw.Stop();
        //Console.Write($"Brücken des Graphen multithread: ({sw.ElapsedMilliseconds} ms): ");
        //Console.WriteLine(JsonSerializer.Serialize(separators));
    }

    /// <summary>
    /// Gibt die Distanzmatrix als Tabelle in der Konsole aus.
    /// </summary>
    private static void PrintDistanceMatrix(int?[,] matrix)
    {
        Console.Write(new string(' ', 3));
        for (int col = 0; col < matrix.GetLength(1); col++)
            Console.Write(col.ToString().PadLeft(3));
        Console.WriteLine();

        for (int row = 0; row < matrix.GetLength(0); row++)
        {
            Console.Write(row.ToString().PadLeft(3));
            for (int col = 0; col < matrix.GetLength(1); col++)
                Console.Write((matrix[row, col]?.ToString() ?? "").PadLeft(3));
            Console.WriteLine();
        }
    }

    private static void PrintInColor(string text, ConsoleColor color)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = oldColor;
    }
}