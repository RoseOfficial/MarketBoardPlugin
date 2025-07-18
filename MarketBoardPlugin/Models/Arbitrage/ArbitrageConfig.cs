// <copyright file="ArbitrageConfig.cs" company="Florian Maunier">
// Copyright (c) Florian Maunier. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.Arbitrage
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;

  /// <summary>
  /// Configuration settings for arbitrage analysis.
  /// </summary>
  public class ArbitrageConfig
  {
    /// <summary>
    /// Gets or sets the minimum profit per unit in gil to consider an opportunity.
    /// </summary>
    public long MinProfitPerUnit { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the minimum total profit in gil to consider an opportunity.
    /// </summary>
    public long MinTotalProfit { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the minimum return on investment percentage.
    /// </summary>
    public double MinRoiPercentage { get; set; } = 10.0;

    /// <summary>
    /// Gets or sets the maximum number of opportunities to display.
    /// </summary>
    public int MaxOpportunities { get; set; } = 100;

    /// <summary>
    /// Gets or sets the auto-refresh interval in seconds (0 = manual only).
    /// </summary>
    public int AutoRefreshIntervalSeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to include HQ items in analysis.
    /// </summary>
    public bool IncludeHqItems { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include NQ items in analysis.
    /// </summary>
    public bool IncludeNqItems { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of item IDs to specifically analyze (empty = all items).
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for configuration")]
    public IList<long> SpecificItemIds { get; set; } = new List<long>();

    /// <summary>
    /// Gets or sets the list of world names to exclude from analysis.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for configuration")]
    public IList<string> ExcludedWorlds { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the tax rate percentage to apply for profit calculations.
    /// </summary>
    public double TaxRatePercentage { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets a value indicating whether to consider market velocity in analysis.
    /// </summary>
    public bool ConsiderMarketVelocity { get; set; } = false;

    /// <summary>
    /// Gets or sets the minimum sale velocity to consider for opportunities.
    /// </summary>
    public double MinSaleVelocity { get; set; } = 0.1;
  }
}