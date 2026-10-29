//+------------------------------------------------------------------+
//| MT4Recorder.mq4 - 唯讀帳戶紀錄器 (不下單、不改單、不平倉)         |
//| 每 2 秒輸出 JSON 到 Common\Files\mt4dash.json                    |
//+------------------------------------------------------------------+
#property copyright "kin"
#property version   "1.00"
#property strict

input int    IntervalSec = 2;
input string OutFile     = "mt4dash.json";

string g_gold = "";

string Esc(string s)
{
   StringReplace(s, "\\", "\\\\");
   StringReplace(s, "\"", "\\\"");
   return s;
}
string D(double v, int dg = 2) { return DoubleToString(v, dg); }
string Q(string s) { return "\"" + Esc(s) + "\""; }
string T(datetime t) { return Q(TimeToString(t, TIME_DATE | TIME_SECONDS)); }

string TypeName(int t)
{
   switch(t)
   {
      case OP_BUY:       return "BUY";
      case OP_SELL:      return "SELL";
      case OP_BUYLIMIT:  return "BUY LIMIT";
      case OP_SELLLIMIT: return "SELL LIMIT";
      case OP_BUYSTOP:   return "BUY STOP";
      case OP_SELLSTOP:  return "SELL STOP";
   }
   return "OTHER";
}

string FindGold()
{
   string cands[] = {"XAUUSD", "XAUUSDm", "XAUUSD.", "XAUUSD.a", "XAUUSD.r", "XAUUSDpro", "GOLD", "GOLDm", "XAUUSD+", "XAUUSD#"};
   for(int i = 0; i < ArraySize(cands); i++)
   {
      if(SymbolSelect(cands[i], true) && MarketInfo(cands[i], MODE_BID) > 0)
         return cands[i];
   }
   // 掃描所有商品名稱含 XAU 或 GOLD
   int n = SymbolsTotal(false);
   for(int j = 0; j < n; j++)
   {
      string s = SymbolName(j, false);
      string u = s;
      StringToUpper(u);
      if(StringFind(u, "XAUUSD") >= 0 || StringFind(u, "GOLD") == 0)
      {
         SymbolSelect(s, true);
         return s;
      }
   }
   return "";
}

int OnInit()
{
   g_gold = FindGold();
   EventSetTimer(MathMax(1, IntervalSec));
   WriteJson();
   return INIT_SUCCEEDED;
}

void OnDeinit(const int reason) { EventKillTimer(); }
void OnTick() {}
void OnTimer() { WriteJson(); }

void WriteJson()
{
   RefreshRates();
   datetime now = TimeCurrent();
   datetime dayStart = now - (now % 86400);

   string j = "{";
   j += "\"account\":" + IntegerToString(AccountNumber()) + ",";
   j += "\"server\":" + Q(AccountServer()) + ",";
   j += "\"name\":" + Q(AccountName()) + ",";
   j += "\"currency\":" + Q(AccountCurrency()) + ",";
   j += "\"balance\":" + D(AccountBalance()) + ",";
   j += "\"equity\":" + D(AccountEquity()) + ",";
   j += "\"margin\":" + D(AccountMargin()) + ",";
   j += "\"freeMargin\":" + D(AccountFreeMargin()) + ",";
   j += "\"connected\":" + (IsConnected() ? "true" : "false") + ",";

   // 持倉
   double lots = 0, floating = 0;
   string pos = "";
   int cnt = 0;
   for(int i = 0; i < OrdersTotal(); i++)
   {
      if(!OrderSelect(i, SELECT_BY_POS, MODE_TRADES)) continue;
      double p = OrderProfit() + OrderSwap() + OrderCommission();
      int dg = (int)MarketInfo(OrderSymbol(), MODE_DIGITS);
      double cur = (OrderType() == OP_BUY) ? MarketInfo(OrderSymbol(), MODE_BID) : MarketInfo(OrderSymbol(), MODE_ASK);
      if(OrderType() <= OP_SELL) { lots += OrderLots(); floating += p; }
      if(cnt > 0) pos += ",";
      pos += "{\"ticket\":" + IntegerToString(OrderTicket())
           + ",\"symbol\":" + Q(OrderSymbol())
           + ",\"type\":" + Q(TypeName(OrderType()))
           + ",\"lots\":" + D(OrderLots())
           + ",\"openPrice\":" + D(OrderOpenPrice(), dg)
           + ",\"currentPrice\":" + D(cur, dg)
           + ",\"sl\":" + D(OrderStopLoss(), dg)
           + ",\"tp\":" + D(OrderTakeProfit(), dg)
           + ",\"profit\":" + D(p)
           + ",\"openTime\":" + T(OrderOpenTime()) + "}";
      cnt++;
   }
   j += "\"totalLots\":" + D(lots) + ",";
   j += "\"floatingPL\":" + D(floating) + ",";
   j += "\"positions\":[" + pos + "],";

   // 當日已平倉 (以券商伺服器日)
   double realized = 0;
   string cl = "";
   int cc = 0;
   for(int k = 0; k < OrdersHistoryTotal(); k++)
   {
      if(!OrderSelect(k, SELECT_BY_POS, MODE_HISTORY)) continue;
      if(OrderType() > OP_SELL) continue;           // 排除掛單/出入金
      if(OrderCloseTime() < dayStart) continue;
      double p = OrderProfit() + OrderSwap() + OrderCommission();
      realized += p;
      if(cc > 0) cl += ",";
      cl += "{\"ticket\":" + IntegerToString(OrderTicket())
          + ",\"symbol\":" + Q(OrderSymbol())
          + ",\"type\":" + Q(TypeName(OrderType()))
          + ",\"lots\":" + D(OrderLots())
          + ",\"profit\":" + D(p)
          + ",\"closeTime\":" + T(OrderCloseTime()) + "}";
      cc++;
   }
   j += "\"todayRealized\":" + D(realized) + ",";
   j += "\"todayPL\":" + D(realized + floating) + ",";
   j += "\"closedToday\":[" + cl + "],";

   if(g_gold == "") g_gold = FindGold();
   double gb = 0, ga = 0;
   int gd = 2;
   if(g_gold != "")
   {
      gb = MarketInfo(g_gold, MODE_BID);
      ga = MarketInfo(g_gold, MODE_ASK);
      gd = (int)MarketInfo(g_gold, MODE_DIGITS);
   }
   j += "\"gold\":{\"symbol\":" + Q(g_gold) + ",\"bid\":" + D(gb, gd) + ",\"ask\":" + D(ga, gd) + "},";
   j += "\"serverTime\":" + T(now) + ",";
   j += "\"localTime\":" + T(TimeLocal()) + ",";
   j += "\"unix\":" + IntegerToString((long)TimeGMT());
   j += "}";

   // 原子寫入：先寫暫存檔再改名
   string tmp = OutFile + ".tmp";
   int h = FileOpen(tmp, FILE_WRITE | FILE_TXT | FILE_COMMON | FILE_ANSI);
   if(h == INVALID_HANDLE) { Print("MT4Recorder: FileOpen 失敗 ", GetLastError()); return; }
   FileWriteString(h, j);
   FileClose(h);
   if(!FileMove(tmp, FILE_COMMON, OutFile, FILE_COMMON | FILE_REWRITE))
      Print("MT4Recorder: FileMove 失敗 ", GetLastError());
}
//+------------------------------------------------------------------+
