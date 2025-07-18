// <copyright file="ArbitrageAnalyzer.cs" company="Florian Maunier">
// Copyright (c) Florian Maunier. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Helpers
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Lumina.Excel.Sheets;
  using Lumina.Extensions;
  using MarketBoardPlugin.Models.Arbitrage;
  using MarketBoardPlugin.Models.Universalis;

  /// <summary>
  /// Service for analyzing cross-server arbitrage opportunities.
  /// </summary>
  public class ArbitrageAnalyzer
  {
    private readonly UniversalisClient client;
    private readonly MBPlugin plugin;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArbitrageAnalyzer"/> class.
    /// </summary>
    /// <param name="client">The Universalis client for fetching market data.</param>
    /// <param name="plugin">The plugin instance for logging.</param>
    public ArbitrageAnalyzer(UniversalisClient client, MBPlugin plugin)
    {
      this.client = client;
      this.plugin = plugin;
    }

    /// <summary>
    /// Analyzes arbitrage opportunities for the specified items across worlds in the same data center.
    /// </summary>
    /// <param name="itemIds">The item IDs to analyze.</param>
    /// <param name="worldNames">The world names to analyze.</param>
    /// <param name="config">The arbitrage configuration settings.</param>
    /// <param name="homeWorldName">The player's home world name for filtering sell opportunities.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An arbitrage analysis containing found opportunities.</returns>
    public async Task<ArbitrageAnalysis> AnalyzeArbitrageOpportunities(IEnumerable<uint> itemIds, IEnumerable<string> worldNames, ArbitrageConfig config, string homeWorldName, CancellationToken cancellationToken)
    {
      ArgumentNullException.ThrowIfNull(config);

      var stopwatch = Stopwatch.StartNew();
      var analysis = new ArbitrageAnalysis
      {
        AnalysisTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
        AnalyzedWorlds = worldNames.ToList(),
        MinProfitThreshold = config.MinTotalProfit,
        MinRoiThreshold = config.MinRoiPercentage,
      };

      try
      {
        var filteredWorldNames = worldNames.Where(world => !config.ExcludedWorlds.Contains(world)).ToList();
        var itemIdsList = itemIds.ToList();

        analysis.TotalItemsAnalyzed = itemIdsList.Count;

        if (filteredWorldNames.Count == 0)
        {
          this.plugin.Log.Warning("No worlds available for arbitrage analysis after filtering");
          return analysis;
        }

        if (string.IsNullOrEmpty(homeWorldName))
        {
          this.plugin.Log.Warning("Player's home world not provided, cannot filter arbitrage opportunities");
          return analysis;
        }

        // Get the player's data center name (for logging purposes)
        var dataCenterName = homeWorldName; // Use home world as fallback for data center name

        this.plugin.Log.Information($"Fetching market data for {itemIdsList.Count} items across {filteredWorldNames.Count} worlds");

        var allMarketData = await this.client.GetMultiItemMultiWorldMarketData(
          itemIdsList,
          filteredWorldNames,
          listingCount: 10,
          historyCount: 5,
          cancellationToken).ConfigureAwait(false);

        this.plugin.Log.Information($"Retrieved market data for {allMarketData.Count} items");

        var opportunities = new List<ArbitrageOpportunity>();

        foreach (var itemData in allMarketData)
        {
          var itemId = itemData.Key;
          var worldData = itemData.Value;

          var itemOpportunities = this.FindOpportunitiesForItem(
            itemId,
            worldData,
            config,
            dataCenterName,
            homeWorldName);

          opportunities.AddRange(itemOpportunities);
        }

        this.plugin.Log.Information($"Raw opportunities found before filtering: {opportunities.Count}");
        
        var filteredOpportunities = opportunities
          .Where(o => o.TotalProfit >= config.MinTotalProfit)
          .Where(o => o.RoiPercentage >= config.MinRoiPercentage)
          .Where(o => o.ProfitPerUnit >= config.MinProfitPerUnit)
          .OrderByDescending(o => o.TotalProfit)
          .Take(config.MaxOpportunities)
          .ToList();

        this.plugin.Log.Information($"Filtered opportunities: {filteredOpportunities.Count} (MinTotalProfit: {config.MinTotalProfit}, MinROI: {config.MinRoiPercentage}, MinProfitPerUnit: {config.MinProfitPerUnit})");

        analysis.Opportunities = filteredOpportunities;
        analysis.OpportunitiesFound = filteredOpportunities.Count;
      }
      catch (Exception ex)
      {
        this.plugin.Log.Error(ex, "Failed to analyze arbitrage opportunities");
        throw;
      }
      finally
      {
        stopwatch.Stop();
        analysis.AnalysisDurationMs = stopwatch.ElapsedMilliseconds;
      }

      return analysis;
    }

    /// <summary>
    /// Analyzes arbitrage opportunities for a single item across multiple worlds.
    /// </summary>
    /// <param name="itemId">The item ID to analyze.</param>
    /// <param name="worldNames">The world names to analyze.</param>
    /// <param name="config">The arbitrage configuration settings.</param>
    /// <param name="homeWorldName">The player's home world name for filtering sell opportunities.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of arbitrage opportunities for the item.</returns>
    public async Task<IList<ArbitrageOpportunity>> AnalyzeSingleItemArbitrage(uint itemId, IEnumerable<string> worldNames, ArbitrageConfig config, string homeWorldName, CancellationToken cancellationToken)
    {
      var filteredWorldNames = worldNames.Where(world => !config.ExcludedWorlds.Contains(world)).ToList();

      var worldData = await this.client.GetMultiWorldMarketData(
        itemId,
        filteredWorldNames,
        listingCount: 10,
        historyCount: 5,
        cancellationToken).ConfigureAwait(false);

      var opportunities = this.FindOpportunitiesForItem(
        itemId,
        worldData,
        config,
        homeWorldName,
        homeWorldName);

      return opportunities
        .Where(o => o.TotalProfit >= config.MinTotalProfit)
        .Where(o => o.RoiPercentage >= config.MinRoiPercentage)
        .Where(o => o.ProfitPerUnit >= config.MinProfitPerUnit)
        .OrderByDescending(o => o.TotalProfit)
        .ToList();
    }

    private IList<ArbitrageOpportunity> FindOpportunitiesForItem(uint itemId, Dictionary<string, MarketDataResponse?> worldData, ArbitrageConfig config, string dataCenter, string homeWorldName)
    {
      var opportunities = new List<ArbitrageOpportunity>();

      var validWorldData = worldData
        .Where(kvp => kvp.Value != null)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

      if (validWorldData.Count < 2)
      {
        this.plugin.Log.Verbose($"Item {itemId}: Only {validWorldData.Count} worlds have data, skipping");
        return opportunities;
      }

      this.plugin.Log.Verbose($"Item {itemId}: Analyzing {validWorldData.Count} worlds for arbitrage");
      
      // If home world is specified, only allow selling on home world
      if (!string.IsNullOrEmpty(homeWorldName))
      {
        this.plugin.Log.Verbose($"Item {itemId}: Filtering to only sell on home world '{homeWorldName}'");
        
        // Check if home world has market data
        if (!validWorldData.ContainsKey(homeWorldName))
        {
          this.plugin.Log.Verbose($"Item {itemId}: Home world '{homeWorldName}' has no market data, skipping item");
          return opportunities;
        }
      }

      foreach (var buyWorld in validWorldData)
      {
        // If home world is specified, only consider selling on home world
        var sellWorlds = string.IsNullOrEmpty(homeWorldName) 
          ? validWorldData 
          : validWorldData.Where(kvp => kvp.Key == homeWorldName);
          
        foreach (var sellWorld in sellWorlds)
        {
          if (buyWorld.Key == sellWorld.Key)
          {
            continue;
          }

          var buyData = buyWorld.Value;
          var sellData = sellWorld.Value;

          if (config.IncludeNqItems)
          {
            var nqOpportunity = this.AnalyzePriceGap(
              itemId,
              buyWorld.Key,
              sellWorld.Key,
              buyData,
              sellData,
              isHq: false,
              config,
              dataCenter);

            if (nqOpportunity != null)
            {
              opportunities.Add(nqOpportunity);
            }
          }

          if (config.IncludeHqItems)
          {
            var hqOpportunity = this.AnalyzePriceGap(
              itemId,
              buyWorld.Key,
              sellWorld.Key,
              buyData,
              sellData,
              isHq: true,
              config,
              dataCenter);

            if (hqOpportunity != null)
            {
              opportunities.Add(hqOpportunity);
            }
          }
        }
      }

      return opportunities;
    }

    private ArbitrageOpportunity? AnalyzePriceGap(uint itemId, string buyWorld, string sellWorld, MarketDataResponse buyData, MarketDataResponse sellData, bool isHq, ArbitrageConfig config, string dataCenter)
    {
      var buyListings = buyData.Listings.Where(l => l.Hq == isHq).OrderBy(l => l.PricePerUnit).ToList();
      var sellListings = sellData.Listings.Where(l => l.Hq == isHq).OrderByDescending(l => l.PricePerUnit).ToList();

      if (!buyListings.Any() || !sellListings.Any())
      {
        return null;
      }

      var cheapestBuyListing = buyListings.First();
      var highestSellListing = sellListings.First();

      var buyPrice = cheapestBuyListing.PricePerUnit;
      var sellPrice = highestSellListing.PricePerUnit;

      var tax = (long)(sellPrice * (config.TaxRatePercentage / 100.0));
      var profitPerUnit = sellPrice - buyPrice - tax;

      if (profitPerUnit <= 0)
      {
        return null;
      }

      var maxQuantity = Math.Min(cheapestBuyListing.Quantity, highestSellListing.Quantity);
      var totalProfit = profitPerUnit * maxQuantity;
      var roiPercentage = (double)profitPerUnit / buyPrice * 100.0;

      if (config.ConsiderMarketVelocity)
      {
        var velocity = isHq ? sellData.SaleVelocityHq : sellData.SaleVelocityNq;
        if (velocity < config.MinSaleVelocity)
        {
          return null;
        }
      }

      // Get the actual item name
      var itemName = "Unknown Item";
      try
      {
        var item = this.plugin.DataManager.GetExcelSheet<Item>()?.GetRow(itemId);
        if (item != null && !item.Value.Name.IsEmpty)
        {
          itemName = item.Value.Name.ExtractText();
        }
      }
      catch (Exception ex)
      {
        this.plugin.Log.Warning(ex, $"Failed to get name for item {itemId}");
      }

      return new ArbitrageOpportunity
      {
        ItemId = (long)itemId,
        ItemName = string.IsNullOrEmpty(itemName) ? $"Item {itemId}" : itemName,
        BuyWorld = buyWorld,
        BuyPrice = buyPrice,
        BuyQuantity = cheapestBuyListing.Quantity,
        SellWorld = sellWorld,
        SellPrice = sellPrice,
        SellQuantity = highestSellListing.Quantity,
        ProfitPerUnit = profitPerUnit,
        TotalProfit = totalProfit,
        RoiPercentage = roiPercentage,
        MaxQuantity = maxQuantity,
        IsHq = isHq,
        Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
        DataCenter = dataCenter,
      };
    }
  }
}