using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramEAManager
{
    // Parsed signal data
    public class ParsedSignalData
    {
        public string Symbol { get; set; } = "";
        public string Direction { get; set; } = "";
        public double EntryPrice { get; set; } = 0;
        public double StopLoss { get; set; } = 0;
        public double TakeProfit1 { get; set; } = 0;
        public double TakeProfit2 { get; set; } = 0;
        public double TakeProfit3 { get; set; } = 0;
        public string OriginalSymbol { get; set; } = "";
        public string FinalSymbol { get; set; } = "";
    }
}
