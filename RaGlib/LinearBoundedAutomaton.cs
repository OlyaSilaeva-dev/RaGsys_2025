using RaGlib.Automata;
using RaGlib.Grammars;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaGlib
{
    public class LBA : TuringMachine
    {
        private LBA(List<string> states, List<string> sigma, List<string> finalStates, string q0)
            : base(states, sigma, finalStates, q0) { }

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
            var states = new List<string> { "t0", "t1", "s0", "s1", "r0", "r1" };

            foreach (var p in productions)
            {
                foreach (var t in T)
                {
                    if (p.LHS.Contains(t))
                    {
                        states.Add("s" + t);
                    }
                }
            }

            var sigma = new List<string>(V) { "$" };
            var lba = new LBA(states, sigma, new List<string> { "halt" }, "t0");

            string S = grammar.StartSymbol.symbol;

            //(#,t_0) → (#,t_1,1)
            lba.AddRule("t0", "$", "$", Direction.Right, "t1");

            //(a,t_1) → (a,t_1,1) для всех a в V_T
            foreach (var t in T)
            {
                lba.AddRule("t1", "a", "a", Direction.Right, "t1");
            }

            //(#,t_1) → (#,s_0,-1)
            lba.AddRule("t1", "$", "$", Direction.Left, "s0");

            //(A,s_0) → (A,s_0,1) для всех A в V
            //(A,s_0) → (A,s_0,-1) для всех A в V
            foreach (var A in V)
            {
                lba.AddRule("s0", A, A, Direction.Right, "s0");
                lba.AddRule("s0", A, A, Direction.Left, "s0");
            }

            //(B,s_0) → (A,s_0,0) для всех правил A → B в G
            //(C,s_0) → (A,s_A,1) для всех правил AB → CD в G
            //(D,s_A) → (B,s_0,0) для всех правил AB → CD в G
            //(S,s_0) → (S,r_0,-1) для всех правил S → SA  в G

            //(S,r_1) → (#,s_1,1),
            //(A, s_1) → (S, s_0, 0),
            foreach (var p in productions)
            {
                var rhs = p.RHS;
                var lhs = p.LHS;

                if (lhs.Count == 1 && rhs.Count == 1)
                {
                    lba.AddRule("s0", rhs[0].symbol, lhs[0].symbol, Direction.Stay, "s0");
                }

                if (p.LHS.Count == 2 && p.RHS.Count == 2)
                {
                    var A = lhs[0].symbol;
                    var B = lhs[1].symbol;
                    var C = rhs[0].symbol;
                    var D = rhs[1].symbol;

                    var sA = "s" + A;
                    lba.AddRule("s0", C, A, Direction.Right, sA);
                    lba.AddRule(sA, D, B, Direction.Stay, "s0");
                }


                if (lhs.Count == 1 && rhs.Count == 2 && (lhs[0] == rhs[0]))
                {
                    var A = rhs[1].symbol;
                    lba.AddRule("s0", S, S, Direction.Left, "r0");
                    lba.AddRule("s1", A, S, Direction.Left, "s0");
                }

                lba.AddRule("r1", S, "$", Direction.Right, "s1");

            }

            //(#,r_0) → (#,r_1,1)
            lba.AddRule("r0", "$", "$", Direction.Right, "r1");

            //(#,s_1) → (#,t_0,1)
            lba.AddRule("s1", "$", "$", Direction.Right, "t0");


            /*var sigma = new List<string>(V) { "$" };

            var states = new List<string>
            {
                "t0", "t1",
                "s0", "halt", "reject"
            };

            var lba = new LBA(states, sigma, new List<string> { "halt" }, "t0");
            string S = grammar.StartSymbol.symbol;

            // =======================
            // 1. ИНИЦИАЛИЗАЦИЯ
            // =======================
            lba.AddRule("t0", "$", "$", Direction.Right, "t1");
            foreach (var x in V) lba.AddRule("t1", x, x, Direction.Right, "t1");
            lba.AddRule("t1", "$", "$", Direction.Left, "s0");

            // =======================
            // 2. ПРИМЕНЕНИЕ ПРАВИЛ
            // =======================
            foreach (var p in grammar.CSProductions)
            {
                var lhs = p.LHS.Select(sym => sym.symbol).ToList();
                var rhs = p.RHS.Select(sym => sym.symbol).ToList();

                if (lhs.Count == 1 && rhs.Count == 1)
                {
                    // Однобуквенные правила: A -> B
                    lba.AddRule("s0", lhs[0], rhs[0], Direction.Stay, "s0");
                    continue;
                }

                // Для правил длиной >=2 создаём цепочку промежуточных состояний
                string prevState = "s0";
                for (int i = 0; i < lhs.Count; i++)
                {
                    string currState = $"scan_{string.Join("", lhs)}_{i}";
                    if (!states.Contains(currState)) states.Add(currState);

                    // если символ совпадает, идём дальше
                    lba.AddRule(prevState, lhs[i], lhs[i], Direction.Right, currState);
                    prevState = currState;
                }

                // Теперь замена всей LHS на RHS
                string replaceState = $"replace_{string.Join("", lhs)}";
                if (!states.Contains(replaceState)) states.Add(replaceState);

                // переходим из последнего состояния сканирования в замену
                lba.AddRule(prevState, lhs.Last(), lhs.Last(), Direction.Left, replaceState);

                // Последовательно заменяем символы
                for (int i = 0; i < lhs.Count; i++)
                {
                    string s = replaceState + $"_{i}";
                    if (!states.Contains(s)) states.Add(s);
                    lba.AddRule(i == 0 ? replaceState : replaceState + $"_{i - 1}", lhs[i], rhs[i], Direction.Right, s);
                }

                // Возврат в s0
                lba.AddRule(replaceState + $"_{lhs.Count - 1}", lhs.Last(), rhs.Last(), Direction.Stay, "s0");
            }

            // =======================
            // 3. ФИНАЛЬНАЯ ПРОВЕРКА
            // =======================
            foreach (var t in VT) lba.AddRule("s0", t, t, Direction.Right, "s0");
            foreach (var nt in VN) lba.AddRule("s0", nt, nt, Direction.Stay, "reject");
            lba.AddRule("s0", "$", "$", Direction.Stay, "halt");

            // =======================
            // 4. НЕОПРЕДЕЛЕННЫЕ ПЕРЕХОДЫ
            // =======================
            foreach (var st in states)
            {
                foreach (var sym in sigma)
                {
                    if (!lba.HasRule(st, sym))
                        lba.AddRule(st, sym, sym, Direction.Stay, "reject");
                }
            }
            */
            return lba;
        }
    }
}
