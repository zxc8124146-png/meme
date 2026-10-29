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

string TfJson(string sym, int tf, string name)
{
   int dg = (int)MarketInfo(sym, MODE_DIGITS);
   double close = iClose(sym, tf, 0);
   double up = iBands(sym, tf, 20, 2, 0, PRICE_CLOSE, MODE_UPPER, 0);
   double lo = iBands(sym, tf, 20, 2, 0, PRICE_CLOSE, MODE_LOWER, 0);
   double bbPos = (up - lo) > 0 ? (close - lo) / (up - lo) : 0.5;   // 0=下軌 1=上軌
   double buyVol = 0, sellVol = 0, sumVol = 0;
   int n = MathMin(20, iBars(sym, tf) - 1);
   for(int i = 1; i <= n; i++)
   {
      double v = (double)iVolume(sym, tf, i);
      sumVol += v;
      if(iClose(sym, tf, i) > iOpen(sym, tf, i)) buyVol += v;
      else if(iClose(sym, tf, i) < iOpen(sym, tf, i)) sellVol += v;
   }
   string j = "\"" + name + "\":{";
   j += "\"close\":" + D(close, dg);
   j += ",\"rsi\":" + D(iRSI(sym, tf, 14, PRICE_CLOSE, 0));
   j += ",\"macd\":" + D(iMACD(sym, tf, 12, 26, 9, PRICE_CLOSE, MODE_MAIN, 0), dg + 1);
   j += ",\"macdSignal\":" + D(iMACD(sym, tf, 12, 26, 9, PRICE_CLOSE, MODE_SIGNAL, 0), dg + 1);
   j += ",\"ema20\":" + D(iMA(sym, tf, 20, 0, MODE_EMA, PRICE_CLOSE, 0), dg);
   j += ",\"ema50\":" + D(iMA(sym, tf, 50, 0, MODE_EMA, PRICE_CLOSE, 0), dg);
   j += ",\"ema200\":" + D(iMA(sym, tf, 200, 0, MODE_EMA, PRICE_CLOSE, 0), dg);
   j += ",\"atr\":" + D(iATR(sym, tf, 14, 0), dg);
   j += ",\"bbPos\":" + D(bbPos, 3);
   j += ",\"lastVol\":" + D((double)iVolume(sym, tf, 1), 0);
   j += ",\"avgVol\":" + D(n > 0 ? sumVol / n : 0, 1);
   j += ",\"buyVol\":" + D(buyVol, 0);
   j += ",\"sellVol\":" + D(sellVol, 0);
   j += "}";
   return j;
}

string BarsJson(string sym, int tf, string name, int count)
{
   int dg = (int)MarketInfo(sym, MODE_DIGITS);
   int n = MathMin(count, iBars(sym, tf));
   string j = "\"" + name + "\":[";
   for(int i = n - 1; i >= 0; i--)
   {
      j += "[" + IntegerToString((long)iTime(sym, tf, i)) + "," + D(iOpen(sym, tf, i), dg) + "," + D(iHigh(sym, tf, i), dg) + ","
         + D(iLow(sym, tf, i), dg) + "," + D(iClose(sym, tf, i), dg) + ","
         + D(iMA(sym, tf, 20, 0, MODE_EMA, PRICE_CLOSE, i), dg) + "," + D(iMA(sym, tf, 50, 0, MODE_EMA, PRICE_CLOSE, i), dg) + "]";
      if(i > 0) j += ",";
   }
   return j + "]";
}

string g_suffix = "";
string FindSym(string base)
{
   string c[4];
   c[0] = base + g_suffix; c[1] = base; c[2] = base + "m"; c[3] = base + ".";
   for(int i = 0; i < 4; i++)
      if(StringLen(c[i]) > 0 && SymbolSelect(c[i], true) && MarketInfo(c[i], MODE_BID) > 0) return c[i];
   return "";
}

string MarketsJson()
{
   string groups[32] = {
      "USDX", "DXY", "DX", "USDIDX",  "US500", "SPX500", "SP500", "US500Cash",  "USOIL", "WTI", "XTIUSD", "CL",
      "EURUSD", "", "", "",  "XAGUSD", "SILVER", "", "",  "BTCUSD", "BTCUSDT", "", "",  "USDJPY", "", "", "",  "US30", "DJ30", "WS30", "" };
   string j = "\"markets\":[";
   int cnt = 0;
   for(int g = 0; g < 8; g++)
   {
      string sym = "";
      for(int k = 0; k < 4 && sym == ""; k++) if(groups[g * 4 + k] != "") sym = FindSym(groups[g * 4 + k]);
      if(sym == "") continue;
      int dg = (int)MarketInfo(sym, MODE_DIGITS);
      double bid = MarketInfo(sym, MODE_BID);
      double prev = iClose(sym, PERIOD_D1, 1);
      double chg = prev > 0 ? (bid - prev) / prev * 100.0 : 0;
      if(cnt > 0) j += ",";
      j += "{\"name\":" + Q(groups[g * 4]) + ",\"symbol\":" + Q(sym) + ",\"bid\":" + D(bid, dg) + ",\"chg\":" + D(chg, 2) + "}";
      cnt++;
   }
   return j + "]";
}

string DailyJson(string sym)
{
   int dg = (int)MarketInfo(sym, MODE_DIGITS);
   double ph = iHigh(sym, PERIOD_D1, 1), pl = iLow(sym, PERIOD_D1, 1), pc = iClose(sym, PERIOD_D1, 1);
   double pp = (ph + pl + pc) / 3.0;
   string j = "\"daily\":{";
   j += "\"open\":" + D(iOpen(sym, PERIOD_D1, 0), dg) + ",\"high\":" + D(iHigh(sym, PERIOD_D1, 0), dg) + ",\"low\":" + D(iLow(sym, PERIOD_D1, 0), dg);
   j += ",\"prevHigh\":" + D(ph, dg) + ",\"prevLow\":" + D(pl, dg) + ",\"prevClose\":" + D(pc, dg);
   j += ",\"atrD1\":" + D(iATR(sym, PERIOD_D1, 14, 1), dg);
   j += ",\"pp\":" + D(pp, dg);
   j += ",\"r1\":" + D(2 * pp - pl, dg) + ",\"s1\":" + D(2 * pp - ph, dg);
   j += ",\"r2\":" + D(pp + (ph - pl), dg) + ",\"s2\":" + D(pp - (ph - pl), dg);
   j += ",\"r3\":" + D(ph + 2 * (pp - pl), dg) + ",\"s3\":" + D(pl - 2 * (ph - pp), dg);
   return j + "}";
}

string ClosedWeekJson(datetime now)
{
   datetime from = now - 7 * 86400;
   string j = "\"closedWeek\":[";
   int cnt = 0;
   for(int k = OrdersHistoryTotal() - 1; k >= 0 && cnt < 500; k--)
   {
      if(!OrderSelect(k, SELECT_BY_POS, MODE_HISTORY)) continue;
      if(OrderType() > OP_SELL) continue;
      if(OrderCloseTime() < from) continue;
      if(cnt > 0) j += ",";
      j += "[" + IntegerToString((long)OrderCloseTime()) + "," + D(OrderProfit() + OrderSwap() + OrderCommission()) + "," + Q(OrderSymbol()) + "," + Q(TypeName(OrderType())) + "," + D(OrderLots()) + "]";
      cnt++;
   }
   return j + "]";
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
   if(g_gold != "")
      j += "\"tech\":{" + TfJson(g_gold, PERIOD_M15, "M15") + "," + TfJson(g_gold, PERIOD_H1, "H1") + "," + TfJson(g_gold, PERIOD_H4, "H4") + "},";
   if(g_gold != "")
   {
      int gp = StringFind(g_gold, "XAUUSD");
      g_suffix = gp >= 0 ? StringSubstr(g_gold, gp + 6) : "";
      j += "\"bars\":{" + BarsJson(g_gold, PERIOD_M15, "M15", 120) + "," + BarsJson(g_gold, PERIOD_H1, "H1", 120) + "," + BarsJson(g_gold, PERIOD_H4, "H4", 120) + "},";
      j += DailyJson(g_gold) + ",";
   }
   j += MarketsJson() + ",";
   j += ClosedWeekJson(now) + ",";
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
