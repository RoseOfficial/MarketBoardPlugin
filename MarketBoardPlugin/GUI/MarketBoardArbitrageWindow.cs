// <copyright file="MarketBoardArbitrageWindow.cs" company="Florian Maunier">
// Copyright (c) Florian Maunier. All rights reserved.
// </copyright>

namespace MarketBoardPlugin.GUI
{
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.Linq;
  using System.Numerics;
  using System.Threading;
  using System.Threading.Tasks;
  using Dalamud.Interface;
  using Dalamud.Interface.Colors;
  using Dalamud.Interface.ManagedFontAtlas;
  using Dalamud.Interface.Windowing;
  using ImGuiNET;
  using Lumina.Excel.Sheets;
  using Lumina.Extensions;
  using MarketBoardPlugin.Helpers;
  using MarketBoardPlugin.Models.Arbitrage;

  /// <summary>
  /// The arbitrage window for cross-server market analysis.
  /// </summary>
  public class MarketBoardArbitrageWindow : Window, IDisposable
  {
    private readonly MBPlugin plugin;
    private readonly ArbitrageAnalyzer analyzer;
    private readonly IFontHandle defaultFontHandle;
    private readonly List<(string, string)> worldList = new List<(string, string)>();
    private ArbitrageConfig Config => this.plugin.Config.ArbitrageConfig;

    private bool worldsInitialized = false;
    private ArbitrageAnalysis? currentAnalysis;
    private Task? currentAnalysisTask;
    private CancellationTokenSource? currentAnalysisCancellationTokenSource;
    private bool isAnalyzing;
    private float analysisProgress;
    private string statusMessage = "Ready to analyze";

    private int sortColumn = 6;
    private bool sortDescending = true;
    private string searchFilter = string.Empty;
    private bool showOnlyProfitable = true;

    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBoardArbitrageWindow"/> class.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="analyzer">The arbitrage analyzer service.</param>
    public MarketBoardArbitrageWindow(MBPlugin plugin, ArbitrageAnalyzer analyzer)
      : base("Cross-Server Arbitrage")
    {
      this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
      this.analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));

      this.Flags = ImGuiWindowFlags.None;
      this.Size = new Vector2(1000, 700);
      this.SizeCondition = ImGuiCond.FirstUseEver;
      this.SizeConstraints = new WindowSizeConstraints
      {
        MinimumSize = new Vector2(800, 500),
        MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
      };

      this.defaultFontHandle = this.plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        e.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(-1)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public override void Draw()
    {
      if (!this.worldsInitialized)
      {
        this.InitializeWorlds();
        this.worldsInitialized = true;
      }

      using (this.defaultFontHandle.Push())
      {
        this.DrawConfigurationSection();
        ImGui.Separator();
        this.DrawAnalysisSection();
        ImGui.Separator();
        this.DrawResultsSection();
      }
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing)
    {
      if (!this.isDisposed && disposing)
      {
        this.currentAnalysisCancellationTokenSource?.Cancel();
        this.currentAnalysisCancellationTokenSource?.Dispose();
        this.defaultFontHandle?.Dispose();
        this.isDisposed = true;
      }
    }

    private void InitializeWorlds()
    {
      try
      {
        var localPlayer = this.plugin.ClientState.LocalPlayer;
        if (localPlayer?.CurrentWorld != null)
        {
          var currentDc = localPlayer.CurrentWorld.Value.DataCenter;
          var dcWorlds = this.plugin.DataManager.GetExcelSheet<World>()
            .Where(w => w.DataCenter.RowId == currentDc.RowId && w.IsPublic)
            .OrderBy(w => w.Name.ExtractText())
            .Select(w => (w.Name.ExtractText(), w.Name.ExtractText()));

          this.worldList.AddRange(dcWorlds);
          this.plugin.Log.Information($"Initialized arbitrage world list with {this.worldList.Count} worlds from DC: {currentDc.Value.Name.ExtractText()}");
        }
        else
        {
          this.plugin.Log.Warning("Could not initialize worlds - LocalPlayer or CurrentWorld is null");
        }
      }
      catch (Exception ex)
      {
        this.plugin.Log.Warning(ex, "Failed to initialize world list for arbitrage window");
      }
    }

    private void DrawConfigurationSection()
    {
      if (ImGui.CollapsingHeader("Configuration", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Columns(3, "ConfigColumns", true);

        ImGui.Text("Profit Thresholds");
        var minProfitPerUnit = (int)this.Config.MinProfitPerUnit;
        if (ImGui.InputInt("Min Profit/Unit", ref minProfitPerUnit, 100))
        {
          this.Config.MinProfitPerUnit = Math.Max(0, minProfitPerUnit);
        }

        var minTotalProfit = (int)this.Config.MinTotalProfit;
        if (ImGui.InputInt("Min Total Profit", ref minTotalProfit, 1000))
        {
          this.Config.MinTotalProfit = Math.Max(0, minTotalProfit);
        }

        var minRoi = (float)this.Config.MinRoiPercentage;
        if (ImGui.InputFloat("Min ROI %", ref minRoi, 1.0f))
        {
          this.Config.MinRoiPercentage = Math.Max(0.0, minRoi);
        }

        ImGui.NextColumn();

        ImGui.Text("Item Filters");
        var includeHq = this.Config.IncludeHqItems;
        if (ImGui.Checkbox("Include HQ Items", ref includeHq))
        {
          this.Config.IncludeHqItems = includeHq;
        }

        var includeNq = this.Config.IncludeNqItems;
        if (ImGui.Checkbox("Include NQ Items", ref includeNq))
        {
          this.Config.IncludeNqItems = includeNq;
        }

        var taxRate = (float)this.Config.TaxRatePercentage;
        if (ImGui.InputFloat("Tax Rate %", ref taxRate, 0.1f))
        {
          this.Config.TaxRatePercentage = Math.Max(0.0, Math.Min(100.0, taxRate));
        }

        ImGui.NextColumn();

        ImGui.Text("Analysis Options");
        var maxOpportunities = this.Config.MaxOpportunities;
        if (ImGui.InputInt("Max Results", ref maxOpportunities, 10))
        {
          this.Config.MaxOpportunities = Math.Max(1, Math.Min(1000, maxOpportunities));
        }

        var considerVelocity = this.Config.ConsiderMarketVelocity;
        if (ImGui.Checkbox("Consider Velocity", ref considerVelocity))
        {
          this.Config.ConsiderMarketVelocity = considerVelocity;
        }

        if (this.Config.ConsiderMarketVelocity)
        {
          var minVelocity = (float)this.Config.MinSaleVelocity;
          if (ImGui.InputFloat("Min Velocity", ref minVelocity, 0.1f))
          {
            this.Config.MinSaleVelocity = Math.Max(0.0, minVelocity);
          }
        }

        ImGui.Columns(1);
      }
    }

    private void DrawAnalysisSection()
    {
      if (ImGui.CollapsingHeader("Analysis", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.Text($"Status: {this.statusMessage}");

        if (this.isAnalyzing)
        {
          ImGui.ProgressBar(this.analysisProgress, new Vector2(-1, 0));
          if (ImGui.Button("Cancel"))
          {
            this.currentAnalysisCancellationTokenSource?.Cancel();
          }
        }
        else
        {
          ImGui.Columns(2, "AnalysisButtons", false);

          if (ImGui.Button("Analyze Popular Items"))
          {
            this.StartAnalysis(GetPopularItemIds());
          }

          ImGui.NextColumn();

          if (ImGui.Button("Analyze All Tradeable Items"))
          {
            this.StartAnalysis(GetAllTradeableItemIds());
          }

          ImGui.Columns(1);
        }

        if (this.currentAnalysis != null)
        {
          ImGui.Separator();
          ImGui.Text($"Last Analysis: {DateTimeOffset.FromUnixTimeMilliseconds(this.currentAnalysis.AnalysisTimestamp):yyyy-MM-dd HH:mm:ss}");
          ImGui.Text($"Duration: {this.currentAnalysis.AnalysisDurationMs}ms");
          ImGui.Text($"Items Analyzed: {this.currentAnalysis.TotalItemsAnalyzed}");
          ImGui.Text($"Opportunities Found: {this.currentAnalysis.OpportunitiesFound}");
        }
      }
    }

    private void DrawResultsSection()
    {
      if (this.currentAnalysis?.Opportunities?.Any() != true)
      {
        ImGui.Text("No opportunities found. Run an analysis to see results.");
        return;
      }

      if (ImGui.CollapsingHeader("Results", ImGuiTreeNodeFlags.DefaultOpen))
      {
        ImGui.InputText("Search Filter", ref this.searchFilter, 256);
        ImGui.SameLine();
        ImGui.Checkbox("Profitable Only", ref this.showOnlyProfitable);

        var opportunities = this.FilterAndSortOpportunities();

        if (ImGui.BeginTable("ArbitrageTable", 8, ImGuiTableFlags.Sortable | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
          ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableSetupColumn("Buy World");
          ImGui.TableSetupColumn("Buy Price", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableSetupColumn("Sell World");
          ImGui.TableSetupColumn("Sell Price", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableSetupColumn("Profit/Unit", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableSetupColumn("Total Profit", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableSetupColumn("ROI %", ImGuiTableColumnFlags.DefaultSort);
          ImGui.TableHeadersRow();

          this.HandleTableSorting();

          foreach (var opportunity in opportunities.Take(this.Config.MaxOpportunities))
          {
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.Text($"{opportunity.ItemName}{(opportunity.IsHq ? " (HQ)" : "")}");

            ImGui.TableSetColumnIndex(1);
            ImGui.Text(opportunity.BuyWorld);

            ImGui.TableSetColumnIndex(2);
            ImGui.Text(FormatGil(opportunity.BuyPrice));

            ImGui.TableSetColumnIndex(3);
            ImGui.Text(opportunity.SellWorld);

            ImGui.TableSetColumnIndex(4);
            ImGui.Text(FormatGil(opportunity.SellPrice));

            ImGui.TableSetColumnIndex(5);
            var profitColor = opportunity.ProfitPerUnit > 0 ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed;
            ImGui.TextColored(profitColor, FormatGil(opportunity.ProfitPerUnit));

            ImGui.TableSetColumnIndex(6);
            ImGui.TextColored(profitColor, FormatGil(opportunity.TotalProfit));

            ImGui.TableSetColumnIndex(7);
            ImGui.Text($"{opportunity.RoiPercentage:F1}%");
          }

          ImGui.EndTable();
        }
      }
    }

    private void HandleTableSorting()
    {
      var sortSpecs = ImGui.TableGetSortSpecs();
      if (sortSpecs.SpecsDirty)
      {
        this.sortColumn = sortSpecs.Specs.ColumnIndex;
        this.sortDescending = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending;
        sortSpecs.SpecsDirty = false;
      }
    }

    private List<ArbitrageOpportunity> FilterAndSortOpportunities()
    {
      var opportunities = this.currentAnalysis!.Opportunities.AsEnumerable();

      if (!string.IsNullOrWhiteSpace(this.searchFilter))
      {
        opportunities = opportunities.Where(o => o.ItemName.Contains(this.searchFilter, StringComparison.OrdinalIgnoreCase));
      }

      if (this.showOnlyProfitable)
      {
        opportunities = opportunities.Where(o => o.TotalProfit > 0);
      }

      return this.sortColumn switch
      {
        0 => this.sortDescending ? opportunities.OrderByDescending(o => o.ItemName).ToList() : opportunities.OrderBy(o => o.ItemName).ToList(),
        1 => this.sortDescending ? opportunities.OrderByDescending(o => o.BuyWorld).ToList() : opportunities.OrderBy(o => o.BuyWorld).ToList(),
        2 => this.sortDescending ? opportunities.OrderByDescending(o => o.BuyPrice).ToList() : opportunities.OrderBy(o => o.BuyPrice).ToList(),
        3 => this.sortDescending ? opportunities.OrderByDescending(o => o.SellWorld).ToList() : opportunities.OrderBy(o => o.SellWorld).ToList(),
        4 => this.sortDescending ? opportunities.OrderByDescending(o => o.SellPrice).ToList() : opportunities.OrderBy(o => o.SellPrice).ToList(),
        5 => this.sortDescending ? opportunities.OrderByDescending(o => o.ProfitPerUnit).ToList() : opportunities.OrderBy(o => o.ProfitPerUnit).ToList(),
        6 => this.sortDescending ? opportunities.OrderByDescending(o => o.TotalProfit).ToList() : opportunities.OrderBy(o => o.TotalProfit).ToList(),
        7 => this.sortDescending ? opportunities.OrderByDescending(o => o.RoiPercentage).ToList() : opportunities.OrderBy(o => o.RoiPercentage).ToList(),
        _ => opportunities.OrderByDescending(o => o.TotalProfit).ToList(),
      };
    }

    private void StartAnalysis(IEnumerable<uint> itemIds)
    {
      if (this.isAnalyzing)
      {
        return;
      }

      this.currentAnalysisCancellationTokenSource?.Cancel();
      this.currentAnalysisCancellationTokenSource = new CancellationTokenSource();

      var worldNames = this.worldList.Select(w => w.Item2);

      // Get home world on main thread before starting background analysis
      var homeWorldName = string.Empty;
      try
      {
        var localPlayer = this.plugin.ClientState.LocalPlayer;
        if (localPlayer?.HomeWorld != null)
        {
          homeWorldName = localPlayer.HomeWorld.Value.Name.ExtractText();
        }
      }
      catch (Exception ex)
      {
        this.plugin.Log.Warning(ex, "Failed to get player's home world");
        this.statusMessage = "Error: Could not detect player's home world";
        return;
      }

      if (string.IsNullOrEmpty(homeWorldName))
      {
        this.plugin.Log.Warning("Player's home world not detected");
        this.statusMessage = "Error: Player's home world not detected";
        return;
      }

      this.isAnalyzing = true;
      this.analysisProgress = 0.0f;
      this.statusMessage = $"Starting analysis (selling on {homeWorldName})...";

      this.currentAnalysisTask = Task.Run(async () =>
      {
        try
        {
          this.statusMessage = "Analyzing arbitrage opportunities...";
          this.analysisProgress = 0.1f;

          var analysis = await this.analyzer.AnalyzeArbitrageOpportunities(
            itemIds,
            worldNames,
            this.Config,
            homeWorldName,
            this.currentAnalysisCancellationTokenSource.Token);

          this.currentAnalysis = analysis;
          this.statusMessage = $"Analysis complete. Found {analysis.OpportunitiesFound} opportunities.";
          this.analysisProgress = 1.0f;
        }
        catch (OperationCanceledException)
        {
          this.statusMessage = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
          this.plugin.Log.Error(ex, "Failed to analyze arbitrage opportunities");
          this.statusMessage = "Analysis failed. Check logs for details.";
        }
        finally
        {
          this.isAnalyzing = false;
        }
      });
    }

    private static IEnumerable<uint> GetPopularItemIds()
    {
      return new uint[]
      {
        5, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
      };
    }

    private IEnumerable<uint> GetAllTradeableItemIds()
    {
      try
      {
        var items = this.plugin.DataManager.GetExcelSheet<Item>();
        return items
          .Where(item => item.ItemUICategory.RowId != 0 && !item.IsUntradable)
          .Select(item => item.RowId);
      }
      catch (Exception ex)
      {
        this.plugin.Log.Warning(ex, "Failed to get tradeable item IDs, using fallback list");
        return GetPopularItemIds();
      }
    }

    private static string FormatGil(long amount)
    {
      return amount switch
      {
        >= 1_000_000 => $"{amount / 1_000_000.0:F1}M",
        >= 1_000 => $"{amount / 1_000.0:F1}K",
        _ => amount.ToString("N0", CultureInfo.InvariantCulture),
      };
    }
  }
}