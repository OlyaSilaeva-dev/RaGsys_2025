using RaGlib.Automata;
using RaGlib.Grammars;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;

namespace RaGlib
{
    /// <summary>
    /// Linear Bounded Automat
    /// </summary>
    public class LBA
    {

        private List<string> Tape;
        private int Head;
        private string CurrentState;
        private List<TMTransition> Transitions;
        private List<string> FinalStates;
        private string BlankSymbol = "_";

        public LBA(List<string> states, List<string> sigma, List<string> finalStates, string q0)
        {
            Tape = new List<string>();
            Head = 0;
            CurrentState = q0;
            Transitions = new List<TMTransition>();
            FinalStates = finalStates;
        }

        public void AddRule(string fromState, string read, string write, Direction dir, string toState)
        {
            Transitions.Add(new TMTransition(fromState, read, write, dir, toState));
        }

        /// <summary>
        /// Функция перевода КЗ грамматики в ЛОА
        /// </summary>
        /// <param name="grammar"></param>
        /// <returns></returns>
        public static LBA FromCSG(CSGrammar grammar)
        {
            var T = grammar.Terminals.Select(t => t.symbol).ToList();
            var N = grammar.NonTerminals.Select(nt => nt.symbol).ToList();
            var productions = grammar.CSProductions;

            // Положим V = V_T  ∪ V_N и пусть
            // Σ содержит состояния t0, t1, s0, s1, r0, r1,
            // а также sA для каждого A такого,
            // что существует правило вида AB → CD.
            var V = T.Union(N).ToList();
            var states = new List<string> { "t0", "t1", "s0", "s1", "r0", "r1", "t3", "rejected" };

            foreach (var p in productions)
            {
                // Для правил A -> BC (свертка двух символов в один)
                if (p.LHS.Count == 1 && p.RHS.Count == 2)
                {
                    string sRed = "red_" + p.RHS[0].symbol + "_" + p.RHS[1].symbol;
                    if (!states.Contains(sRed)) states.Add(sRed);
                }
                // Для правил AB -> CD (уже есть в вашем коде)
                if (p.LHS.Count == 2)
                {
                    var A = p.LHS[0].symbol;
                    if (!states.Contains("s" + A)) states.Add("s" + A);
                }
            }

            var sigma = new List<string>(V) { "$" };
            var lba = new LBA(states, sigma, new List<string> { "halt" }, "t0");

            string S = grammar.StartSymbol.symbol;


            // Проход сквозь "_" для всех поисковых состояний
            foreach (var st in states.Where(s => s.StartsWith("s") || s.StartsWith("red") || s.StartsWith("r") || s == "check_empty"))
            {
                // Прыгаем через пустые места
                lba.AddRule(st, "_", "_", Direction.Right, st);
                lba.AddRule(st, "_", "_", Direction.Left, st);

                // Если мы в процессе редукции (например, ищем вторую часть правила), 
                // мы должны уметь перепрыгивать через уже созданные нетерминалы (A, B, S...)
                if (st.StartsWith("s") || st.StartsWith("red"))
                {
                    foreach (var nt in N)
                    {
                        lba.AddRule(st, nt, nt, Direction.Right, st);
                        lba.AddRule(st, nt, nt, Direction.Left, st);
                    }
                }
            }
            //1. (#,t_0) → (#,t_1,1)
            lba.AddRule("t0", "$", "$", Direction.Right, "t1");

            //2. (a,t_1) → (a,t_1,1) для всех a в V_T
            foreach (var t in T)
            {
                lba.AddRule("t1", t, t, Direction.Right, "t1");
            }

            //3. (#,t_1) → (#,s_0,-1)
            lba.AddRule("t1", "$", "$", Direction.Left, "s0");

            //4. (A,s_0) → (A,s_0,1) для всех A в V
            //5. (A,s_0) → (A,s_0,-1) для всех A в V
            foreach (var A in V)
            {
                lba.AddRule("s0", A, A, Direction.Right, "s0");
                lba.AddRule("s0", A, A, Direction.Left, "s0");
            }


            //(A, s_1) → (S, s_0, 0),
            foreach (var p in productions)
            {
                var rhs = p.RHS;
                var lhs = p.LHS;

                // A -> B (6)
                if (lhs.Count == 1 && rhs.Count == 1)
                {
                    lba.AddRule("s0", rhs[0].symbol, lhs[0].symbol, Direction.Stay, "s0");
                }

                // A -> BC (ВАЖНО: это исправление для правил типа S -> A1 A2)
                else if (lhs.Count == 1 && rhs.Count == 2)
                {
                    var A = lhs[0].symbol;
                    var B = rhs[0].symbol;
                    var C = rhs[1].symbol;
                    string sRed = "red_" + B + "_" + C;

                    lba.AddRule("s0", B, A, Direction.Right, sRed);
                    lba.AddRule(sRed, C, "_", Direction.Stay, "s0");
                }

                // AB -> CD (7, 8)
                else if (lhs.Count == 2 && rhs.Count == 2)
                {
                    var A = lhs[0].symbol;
                    var B = lhs[1].symbol;
                    var C = rhs[0].symbol;
                    var D = rhs[1].symbol;

                    var sA = "s" + A;
                    // ВАЖНО: Добавляем эти правила только ОДИН раз для каждого sA
                    if (!lba.Transitions.Any(t => t.FromState == sA && t.ReadSymbol == "_"))
                    {
                        lba.AddRule(sA, "_", "_", Direction.Right, sA);
                    }

                    // Свертка: Заменяем C на A, идем вправо искать D, чтобы заменить его на B
                    lba.AddRule("s0", C, A, Direction.Right, sA);
                    lba.AddRule(sA, D, B, Direction.Stay, "s0");
                }
            }
            // Если нашли S, идем проверять, нет ли мусора
            lba.AddRule("s0", S, S, Direction.Left, "r0");
            lba.AddRule("r0", "$", "$", Direction.Right, "r1");
            lba.AddRule("r1", S, "_", Direction.Right, "check_empty"); // Стираем S и проверяем ленту дальше

            // Идем до правого края, пропуская только "_"
            lba.AddRule("check_empty", "_", "_", Direction.Right, "check_empty");
            lba.AddRule("check_empty", "$", "$", Direction.Stay, "halt");

            return lba;
        }
        /// <summary>
        /// Вывод дельта-правил
        /// </summary>
        public void PrintTransitions()
        {
            Console.WriteLine("\n--- Правила переходов ---");
            foreach (var t in Transitions)
            {
                string dir = t.MoveDirection == Direction.Right ? "R" :
                             t.MoveDirection == Direction.Left ? "L" : "S";
                Console.WriteLine($"δ({t.FromState}, {t.ReadSymbol}) -> ({t.ToState}, {t.WriteSymbol}, {dir})");
            }
        }

        /// <summary>
        /// Проверка на наличие правил по состоянию и читаемому символу
        /// </summary>
        /// <param name="fromState"></param>
        /// <param name="readSymbol"></param>
        /// <returns></returns>
        public bool HasRule(string fromState, string readSymbol)
        {
            return FindTransition(fromState, readSymbol) != null;
        }

        private TMTransition FindTransition(string state, string symbol)
        {
            return Transitions.FirstOrDefault(t => t.FromState == state && t.ReadSymbol == symbol);
        }

        /// <summary>
        /// Проверка цепочки на принадлежность недетерменированному автомату
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public bool Execute(string input)
        {
            List<string> tape = new List<string> { "$" };
            foreach (char c in input) tape.Add(c.ToString());
            tape.Add("$");

            var visited = new HashSet<string>();

            Console.WriteLine($"\n--- Начинаю проверку строки: {input} ---");

            var path = Search("t0", 0, tape, visited);

            if (path != null)
            {
                Console.WriteLine("РЕЗУЛЬТАТ: ACCEPTED");
                Console.WriteLine("Путь успешной ветки:");
                foreach (var step in path) Console.WriteLine(step);
                return true;
            }
            else
            {
                Console.WriteLine("РЕЗУЛЬТАТ: REJECTED");
                return false;
            }
        }

        private List<string> Search(string state, int head, List<string> tape, HashSet<string> visited)
        {
            if (state == "halt")
                return new List<string> { $"[УСПЕХ] Лента: {string.Join("", tape)}" };

            if (state == "rejected") return null;

            string tapeStr = string.Join("", tape);
            string config = $"{state}|{head}|{tapeStr}";

            if (visited.Contains(config)) return null;
            visited.Add(config);

            string currentSymbol = tape[head];

            var rules = Transitions.Where(t => t.FromState == state && t.ReadSymbol == currentSymbol).ToList();

            // РАЗДЕЛЯЕМ ПРАВИЛА ПО ПРИОРИТЕТУ:
            var priorityRules = rules.OrderByDescending(r => {
                // Высший приоритет (2): Свертка (изменение символа, например c -> C или B -> _)
                if (r.ReadSymbol != r.WriteSymbol) return 2;
                // Средний приоритет (1): Переход в состояние редукции (например, s0 -> red_...)
                if (r.FromState != r.ToState) return 1;
                // Низший приоритет (0): Просто движение (s0 -> s0)
                return 0;
            }).ToList();

            foreach (var rule in priorityRules)
            {
                if (rule.ToState == "rejected") continue;

                List<string> nextTape = new List<string>(tape);
                nextTape[head] = rule.WriteSymbol;

                int nextHead = head;
                if (rule.MoveDirection == Direction.Right) nextHead++;
                else if (rule.MoveDirection == Direction.Left) nextHead--;

                if (nextHead < 0 || nextHead >= nextTape.Count) continue;

                var resultPath = Search(rule.ToState, nextHead, nextTape, visited);
                if (resultPath != null)
                {
                    string tapeWithHead = "";
                    for (int i = 0; i < tape.Count; i++)
                    {
                        if (i == head) tapeWithHead += $"[{tape[i]}]";
                        else tapeWithHead += tape[i];
                    }

                    if (resultPath != null)
                    {
                        string dir = rule.MoveDirection.ToString().Substring(0, 1);
                        resultPath.Insert(0, $"{tapeWithHead,-30} | δ({state}, {currentSymbol}) -> ({rule.ToState}, {rule.WriteSymbol}, {dir})");
                        return resultPath;
                    }
                    return resultPath;
                }
            }
            return null;
        }
    }
}
