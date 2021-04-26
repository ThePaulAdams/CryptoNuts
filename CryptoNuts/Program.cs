using System;

using System.Linq;

using Binance.Net;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using Newtonsoft.Json.Linq;
using Binance.Net.Objects.Spot;

namespace CryptoNuts
{
    class Program
    {
        static string baseCurrency, tradeCurrency, key, secret, symbol;
        static int tradeAmount;
        static long? buyOrderId = null;
        static long? sellOrderId = null;

        static void Main()
        {           
            var config = JObject.Parse(System.IO.File.ReadAllText("config.json"));

            baseCurrency = config["baseCurrency"].ToString();
            tradeCurrency = config["tradeCurrency"].ToString();
            tradeAmount = int.Parse(config["tradeAmount"].ToString());
            key = config["key"].ToString();
            secret = config["secret"].ToString();
            symbol = tradeCurrency + baseCurrency;
            InfoLog("Currency: " + baseCurrency + " | Trade: " + tradeCurrency + " | Symbol: " + symbol);

            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(key, secret),
                LogVerbosity = LogVerbosity.None            
            });

            var tick = new System.Timers.Timer();
            tick.Elapsed += Tick_Elapsed;
            tick.Interval = 30000;
            tick.Enabled = true;
            Console.ReadLine();
        }

        private static void Tick_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {          
            using (var client = new BinanceClient())
            {
                //stuff that i just want to see
                var orderBook = client.Spot.Market.GetOrderBook(symbol, 5);
                var vet = client.Spot.Market.Get24HPrice(symbol);
                var lastBid = orderBook.Data.Bids.ToList()[0].Price;
                var lastAsk = orderBook.Data.Asks.ToList()[0].Price;
                var allPairs = client.Spot.Market.GetAllBookPrices();                          
                decimal? tradeCurrencyPrice = client.Spot.Market.Get24HPrice(symbol).Data.LastPrice;
                //check my usdt trading balance
                var tradingBalance = client.General.GetAccountInfo().Data.Balances.First(b => b.Asset == "USDT");


                //get historic candles
                var historicDay = client.Spot.Market.GetKlines(symbol, Binance.Net.Enums.KlineInterval.OneDay).Data.ToList();
                var historicFiveM = client.Spot.Market.GetKlines(symbol, Binance.Net.Enums.KlineInterval.FiveMinutes).Data.AsEnumerable().Reverse().ToList();
                             

                //calculate the 20MA (daily candles)
                var MA20 = historicDay.AsEnumerable().Reverse().Take(20).Sum(x => x.Close) / 20;
                InfoLog(MA20.ToString());

                //is the current last ask more than 10% under the 20 MA daily?
                decimal difference = MA20 - lastAsk;
                decimal percent = difference / MA20 * 100;         

                InfoLog("20MA: " + MA20 + " | buyPrice " + lastAsk + " | difference " + difference + " | percent " + percent);

                //is the current last ask more than 10% under the 20 MA daily?
                //do we already have an order?
                if (buyOrderId == null && percent > 10)
                {
                    //place a market order as we are below 10% of 20day MA                    
                    buyOrderId = client.Spot.Order.PlaceOrder(symbol, OrderSide.Buy, OrderType.Market, tradeAmount, null, null, null, TimeInForce.ImmediateOrCancel).Data.OrderId;  
                    //check if we have made a successfull order, we will have an order number 
                    if(buyOrderId != null)
                    {
                        if (client.Spot.Order.GetOrder(buyOrderId.ToString()).Data.Status == OrderStatus.Filled)
                        {
                            //the order filled so let's set a limit sell back at the current 20 day moving average ( we may never see this fill but YOLO)
                            sellOrderId = client.Spot.Order.PlaceOrder(symbol, OrderSide.Sell, OrderType.Limit, tradeAmount, null, MA20.ToString(), null, TimeInForce.ImmediateOrCancel).Data.OrderId;
                            InfoLog("sell order:" + sellOrderId.ToString());
                        }
                    }
                }
            }
        }

        static void InfoLog(string msg)
        {
            Console.WriteLine(DateTime.Now.ToString() + " I: " + msg);          
        }


    }
}
