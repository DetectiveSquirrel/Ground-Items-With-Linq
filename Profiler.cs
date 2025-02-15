using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore;

namespace Ground_Items_With_Linq;

public class Profiler
{
    public static void LogPerformanceMetrics(Stopwatch profilerModifyLoopStored, Stopwatch profilerTotal,
        Stopwatch profilerModifyStored, Stopwatch profilerIsInFilter)
    {
        profilerModifyLoopStored?.Stop();
        profilerTotal?.Stop();

        var logLines = new List<string>
        {
            // Add headers
            "Profiler | Ticks | Nanoseconds (ns) | Milliseconds (ms)",
            new('-', 60) // Temporary separator length
        };

        // Add profiler results
        AddProfilerResult("Modify Stored", profilerModifyStored);
        AddProfilerResult("Modify Loop Stored", profilerModifyLoopStored);
        AddProfilerResult("Check Is Wanted", profilerIsInFilter);
        AddProfilerResult("Total", profilerTotal);

        // Calculate the maximum width for each column
        var columnWidths = CalculateColumnWidths(logLines);

        // Adjust and print each log line
        foreach (var line in logLines)
            DebugWindow.LogMsg(FormatLine(line, columnWidths), 10);

        void AddProfilerResult(string profilerName, Stopwatch profiler)
        {
            var ticks = profiler.ElapsedTicks;
            var nanoseconds = (double)ticks / Stopwatch.Frequency * 1_000_000_000;
            var milliseconds = profiler.Elapsed.TotalMilliseconds;
            logLines.Add($"{profilerName} | {ticks} | {nanoseconds:N0} | {milliseconds:N2}");
        }

        int[] CalculateColumnWidths(IEnumerable<string> lines)
        {
            return lines
                .Select(line => line.Split('|'))
                .Where(columns => columns.Length == 4)
                .Select(columns => columns.Select(c => c.Trim().Length).ToList())
                .Aggregate((int[]) [0, 0, 0, 0], (max, columns) => max.Zip(columns, Math.Max).ToArray());
        }

        string FormatLine(string line, IReadOnlyList<int> widths)
        {
            var columns = line.Split('|');

            return columns.Length == 4
                ? $"{columns[0].Trim().PadRight(widths[0])} | {columns[1].Trim().PadLeft(widths[1])} | {columns[2].Trim().PadLeft(widths[2])} | {columns[3].Trim().PadLeft(widths[3])}"
                : line; // Return the line as-is for headers and separators
        }
    }
}