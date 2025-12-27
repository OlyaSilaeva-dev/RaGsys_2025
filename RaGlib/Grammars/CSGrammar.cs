using RaGlib.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace RaGlib.Grammars
{
    /// <summary>
    /// Класс Контекстно-зависимой грамматики (CSGGrammar)
    /// </summary>
    public class CSGrammar
    {
        private List<CSProduction> productions { get; set; } = new List<CSProduction>();
        private HashSet<Symbol> terminals { get; set; } = new HashSet<Symbol>();
        private HashSet<Symbol> nonTerminals { get; set; } = new HashSet<Symbol>();
        private Symbol startSymbol { get; set; }

        /// <summary>
        /// Возвращает терминалы грамматики
        /// </summary>
        public IEnumerable<CSProduction> CSProductions => productions;
        /// <summary>
        /// Возвращает терминалы грамматики
        /// </summary>
        public IEnumerable<Symbol> Terminals => terminals;

        /// <summary>
        /// Возвращает нетерминалы грамматики
        /// </summary>
        public IEnumerable<Symbol> NonTerminals => nonTerminals;

        /// <summary>
        /// Возвращает начальный символ грамматики
        /// </summary>
        public Symbol StartSymbol => startSymbol;


        /// <summary>
        /// Конструктор CSGGrammar
        /// </summary>
        /// <param name="terminals">Множество терминалов</param>
        /// <param name="nonTerminals">Множество нетерминалов</param>
        /// <param name="productions">Список продукций грамматики</param>
        /// <param name="startSymbol">Начальный символ грамматики</param>
        public CSGrammar(HashSet<Symbol> terminals, HashSet<Symbol> nonTerminals, List<CSProduction> productions, Symbol startSymbol)
            => (this.terminals, this.nonTerminals, this.productions, this.startSymbol) = (terminals, nonTerminals, productions, startSymbol);

        /// <summary>
        /// Проверяет, является ли грамматика контекстно-зависимой
        /// </summary>
        /// <returns>true, если грамматика контекстно-зависимая, иначе false</returns>
        public bool IsContextSensitive()
        {
            foreach (var p in productions)
            {
                if (p.LHS == null || p.LHS.Count == 0)
                    return false; // LHS не может быть пустым

                if (p.RHS == null || p.RHS.Count == 0)
                    return false; // RHS не может быть пустым

                // Контекстная зависимость: |RHS| >= |LHS|, кроме правил с одним стартовым символом
                if (p.LHS.Count > p.RHS.Count && !(p.LHS.Count == 1 && p.LHS[0] == startSymbol))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Приводит КЗ грамматику к линейной (порядка 2) согласно Лемме 1
        /// </summary>
        /// <returns>Эквивалентная грамматика порядка 2</returns>
        public CSGrammar ToOrderTwo()
        {
            var grammar = this;

            // Преобразуем, пока есть правила с длиной RHS > 2
            while (true)
            {
                // Проверяем текущий максимальный порядок
                int maxOrder = grammar.productions.Max(p => p.RHS.Count);
                if (maxOrder <= 2)
                {
                    break;
                }

                // Уменьшаем порядок на 1
                grammar = ReduceOrderByOne(grammar);
            }

            return grammar;
        }

        /// <summary>
        /// Уменьшает порядок грамматики на 1 согласно Лемме 1
        /// </summary>
        private CSGrammar ReduceOrderByOne(CSGrammar grammar)
        {
            var newTerminals = new HashSet<Symbol>(grammar.terminals);
            var newNonTerminals = new HashSet<Symbol>(grammar.nonTerminals);
            var newProductions = new List<CSProduction>();
            var counter = 1;

            foreach (var p in grammar.productions)
            {
                // Если длина RHS < 3 или это терминальное правило A → a, оставляем без изменений
                if (p.RHS.Count < 3 || (p.LHS.Count == 1 && p.RHS.Count == 1 && grammar.terminals.Contains(p.RHS[0])))
                {
                    newProductions.Add(p);
                    continue;
                }

                // Для правил с длиной RHS ≥ 3
                var φ = p.LHS;
                var ψ = p.RHS;

                // ψ = BCDψ'
                Symbol B = ψ[0];
                Symbol C = ψ[1];
                Symbol D = ψ[2];
                var ψ_prime = ψ.Skip(3).ToList();

                if (φ.Count == 1)  // φ = A
                {
                    Symbol A = φ[0];

                    // Генерируем новые уникальные символы
                    var A1 = new Symbol($"A1_{counter}");
                    var A2 = new Symbol($"A2_{counter}");
                    counter++;

                    newNonTerminals.Add(A1);
                    newNonTerminals.Add(A2);

                    // A → A1A2
                    newProductions.Add(new CSProduction(new List<Symbol> { A }, new List<Symbol> { A1, A2 }));

                    // A1 → BC
                    newProductions.Add(new CSProduction(new List<Symbol> { A1 }, new List<Symbol> { B, C }));

                    // A2 → Dψ'
                    var A2_rhs = new List<Symbol> { D };
                    A2_rhs.AddRange(ψ_prime);
                    newProductions.Add(new CSProduction(new List<Symbol> { A2 }, A2_rhs));
                }
                else  // φ содержит несколько символов: φ = AEφ''
                {
                    Symbol A = φ[0];
                    Symbol E = φ[1];
                    var φ_double_prime = φ.Skip(2).ToList();

                    // Генерируем новые уникальные символы
                    var A_prime = new Symbol($"A_{counter}");
                    var E_prime = new Symbol($"E_{counter}");
                    counter++;

                    newNonTerminals.Add(A_prime);
                    newNonTerminals.Add(E_prime);

                    // AE → A'E'
                    newProductions.Add(new CSProduction(new List<Symbol> { A, E }, new List<Symbol> { A_prime, E_prime }));

                    // A' → B
                    newProductions.Add(new CSProduction(new List<Symbol> { A_prime }, new List<Symbol> { B }));

                    // E'φ'' → CDψ'
                    var E_prime_lhs = new List<Symbol> { E_prime };
                    E_prime_lhs.AddRange(φ_double_prime);

                    var E_prime_rhs = new List<Symbol> { C, D };
                    E_prime_rhs.AddRange(ψ_prime);

                    newProductions.Add(new CSProduction(E_prime_lhs, E_prime_rhs));
                }
            }

            // Удаляем дубликаты правил
            newProductions = newProductions
                .GroupBy(p => $"{string.Join("", p.LHS.Select(s => s.symbol))}→{string.Join("", p.RHS.Select(s => s.symbol))}")
                .Select(g => g.First())
                .ToList();

            return new CSGrammar(newTerminals, newNonTerminals, newProductions, grammar.startSymbol);
        }

        /// <summary>
        /// Преобразует текущую линейную КЗ грамматику в эквивалентную линейно ограниченную грамматику
        /// </summary>
        /// <returns>Новая грамматика CSGGrammar, линейно ограниченная</returns>
        public CSGrammar ToLinearBoundedGrammar()
        {
            var newTerminals = new HashSet<Symbol>(terminals);
            var newNonTerminals = new HashSet<Symbol>(nonTerminals);

            var SPrime = new Symbol(startSymbol.symbol + "'");
            var Q = new Symbol("Q");
            newNonTerminals.Add(SPrime);
            newNonTerminals.Add(Q);

            List<CSProduction> newProductions = new List<CSProduction>
            {
                //S' → S'Q,S' → S,
                new CSProduction(new List<Symbol>{ SPrime }, new List<Symbol>{SPrime, Q}),
                new CSProduction(new List<Symbol>{ SPrime }, new List<Symbol>{startSymbol})
            };

            //QA → AQ,AQ → QA для всех A в V_T  ∪ V_N
            var allSymbols = newNonTerminals.ToList();
            foreach (var A in allSymbols)
            {
                newProductions.Add(new CSProduction(
                    new List<Symbol> { Q, A },
                    new List<Symbol> { A, Q }
                ));
 
                newProductions.Add(new CSProduction(
                    new List<Symbol> { A, Q },
                    new List<Symbol> { Q, A }
                ));
            }

            //A → B если A → B — правило G
            //AB → CD если AB → CD — правило G
            //AQ → BC если A → BC — правило G.
            foreach (var p in productions)
            {
                if ((p.LHS.Count == 1 &&  p.RHS.Count == 1) || (p.LHS.Count == 2) && (p.RHS.Count == 2))
                {
                    newProductions.Add(p);
                }
                else if (p.LHS.Count == 1 && p.RHS.Count == 2)
                {
                    Symbol A = p.LHS[0];
                    CSProduction newCsp = new CSProduction(new List<Symbol> { A, Q }, p.RHS);
                    newProductions.Add(newCsp);
                }
            }

            return new CSGrammar(newTerminals, newNonTerminals, newProductions, SPrime);
        }

        /// <summary>
        /// Выводит грамматику в консоль
        /// </summary>
        public void PrintGrammar()
        {
            Console.WriteLine("=== Контекстно-зависимая грамматика ===");
            Console.WriteLine($"Начальный символ: {startSymbol.symbol}");

            Console.Write("Терминалы: ");
            Console.WriteLine(string.Join(", ", terminals.Select(t => t.symbol)));

            Console.Write("Нетерминалы: ");
            Console.WriteLine(string.Join(", ", nonTerminals.Select(nt => nt.symbol)));

            Console.WriteLine("Продукции:");
            foreach (var p in productions)
            {
                string lhs = string.Join("", p.LHS.Select(s => s.symbol));
                string rhs = string.Join("", p.RHS.Select(s => s.symbol));
                Console.WriteLine($"  {lhs} → {rhs}");
            }
            Console.WriteLine("==============================");
        }
    }
}
