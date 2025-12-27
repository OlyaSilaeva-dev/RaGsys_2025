using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaGlib.Core
{
    /// <summary>
    /// Правило продукции для КЗ грамматики
    /// </summary>
    public class CSProduction
    {
        public List<Symbol> LHS { set; get; } = null; ///< On the Left Hand Side
        public List<Symbol> RHS { set; get; } ///< On the Right Hand Side
        public static int Count = 0;
        public int Id; ///< Production number

        // Конструктор продукции - создает новое правило грамматики с автоматическим присвоением ID
        public CSProduction(List<Symbol> LHS, List<Symbol> RHS)
        {
            Count++;
            Id = Count;
            this.LHS = LHS;
            this.RHS = RHS;
        }
    }
}
