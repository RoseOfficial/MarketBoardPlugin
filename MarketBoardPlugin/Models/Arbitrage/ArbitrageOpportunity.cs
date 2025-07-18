// <copyright file="ArbitrageOpportunity.cs" company="Florian Maunier">
// Copyright (c) Florian Maunier. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.Models.Arbitrage
{
  /// <summary>
  /// Represents a cross-server arbitrage opportunity for an item.
  /// </summary>
  public class ArbitrageOpportunity
  {
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public long ItemId { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the world name where the item can be bought cheaply.
    /// </summary>
    public string BuyWorld { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the lowest buy price per unit.
    /// </summary>
    public long BuyPrice { get; set; }

    /// <summary>
    /// Gets or sets the quantity available at the buy price.
    /// </summary>
    public long BuyQuantity { get; set; }

    /// <summary>
    /// Gets or sets the world name where the item can be sold for higher price.
    /// </summary>
    public string SellWorld { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the highest sell price per unit.
    /// </summary>
    public long SellPrice { get; set; }

    /// <summary>
    /// Gets or sets the quantity that can be sold at the sell price.
    /// </summary>
    public long SellQuantity { get; set; }

    /// <summary>
    /// Gets or sets the profit per unit (sell price - buy price - taxes).
    /// </summary>
    public long ProfitPerUnit { get; set; }

    /// <summary>
    /// Gets or sets the total profit for the available quantity.
    /// </summary>
    public long TotalProfit { get; set; }

    /// <summary>
    /// Gets or sets the return on investment percentage.
    /// </summary>
    public double RoiPercentage { get; set; }

    /// <summary>
    /// Gets or sets the maximum quantity that can be arbitraged.
    /// </summary>
    public long MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is for HQ items.
    /// </summary>
    public bool IsHq { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this opportunity was calculated.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the data center name.
    /// </summary>
    public string DataCenter { get; set; } = string.Empty;
  }
}