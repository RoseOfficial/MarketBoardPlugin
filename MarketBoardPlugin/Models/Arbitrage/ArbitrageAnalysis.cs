// <copyright file="ArbitrageAnalysis.cs" company="Florian Maunier">
// Copyright (c) Florian Maunier. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.Arbitrage
{
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;

  /// <summary>
  /// Represents the results of an arbitrage analysis across multiple items and servers.
  /// </summary>
  public class ArbitrageAnalysis
  {
    /// <summary>
    /// Gets or sets the list of arbitrage opportunities found.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for initialization")]
    public IList<ArbitrageOpportunity> Opportunities { get; set; } = new List<ArbitrageOpportunity>();

    /// <summary>
    /// Gets or sets the timestamp when this analysis was performed.
    /// </summary>
    public long AnalysisTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the data center that was analyzed.
    /// </summary>
    public string DataCenter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of worlds that were included in the analysis.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for initialization")]
    public IList<string> AnalyzedWorlds { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the total number of items analyzed.
    /// </summary>
    public int TotalItemsAnalyzed { get; set; }

    /// <summary>
    /// Gets or sets the number of opportunities found.
    /// </summary>
    public int OpportunitiesFound { get; set; }

    /// <summary>
    /// Gets or sets the minimum profit threshold used in gil.
    /// </summary>
    public long MinProfitThreshold { get; set; }

    /// <summary>
    /// Gets or sets the minimum ROI percentage threshold used.
    /// </summary>
    public double MinRoiThreshold { get; set; }

    /// <summary>
    /// Gets or sets the analysis duration in milliseconds.
    /// </summary>
    public long AnalysisDurationMs { get; set; }
  }
}