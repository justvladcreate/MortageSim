using System;
using System.Collections.Generic;

namespace MortgageSim.Tasks
{
    /// <summary>Один тест, который прогоняется после нажатия «Запустить тесты».</summary>
    public sealed class PythonTestCase
    {
        public string Label;
        public object[] Args;
        public object Expected;

        public PythonTestCase(string label, object expected, params object[] args)
        {
            Label = label;
            Expected = expected;
            Args = args ?? Array.Empty<object>();
        }
    }

    /// <summary>Описание одной ежемесячной задачи.</summary>
    public sealed class PythonTask
    {
        public int Month;
        public string Id;
        public string Title;
        public string Difficulty;
        public string Description;
        public string Hint;
        public string StarterCode;
        public string FunctionName;
        public List<PythonTestCase> Tests = new List<PythonTestCase>();
        public string[] Examples = Array.Empty<string>();
    }

    /// <summary>Результат одного теста.</summary>
    public sealed class PythonTestOutcome
    {
        public bool Passed;
        public string Label;
        public string Expected;
        public string Actual;
        public string Error;

        public string ToConsoleLine()
        {
            if (Passed) return "  [PASS] " + Label;
            if (!string.IsNullOrEmpty(Error)) return "  [FAIL] " + Label + " -> " + Error;
            return "  [FAIL] " + Label + "\n         ожидалось: " + Expected + "\n         получено:  " + Actual;
        }
    }

    /// <summary>Полный результат запуска тестов.</summary>
    public sealed class PythonRunResult
    {
        public bool Success;
        public string Error;
        public List<PythonTestOutcome> Outcomes = new List<PythonTestOutcome>();
        public string ConsoleOutput;

        public int PassedCount
        {
            get
            {
                int n = 0;
                foreach (var o in Outcomes) if (o.Passed) n++;
                return n;
            }
        }

        public int TotalCount => Outcomes.Count;
    }
}
