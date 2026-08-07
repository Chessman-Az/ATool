using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>充值明细窗口 VM：按别名分开展示（一个别名一个明细），每条可设实际金额/佣金，顶部汇总当前别名。</summary>
public partial class RechargeViewModel : ObservableObject
{
    private readonly RechargeRepository _repo;

    /// <summary>“全部”筛选选项。</summary>
    public const string AllAliases = "全部";

    public ObservableCollection<RechargeItemVm> Rows { get; } = [];

    /// <summary>别名筛选列表（全部 + 各别名）。</summary>
    public ObservableCollection<string> Aliases { get; } = [];

    /// <summary>手动添加可选的别名（不含「全部」）。</summary>
    public ObservableCollection<string> ManualAliases { get; } = [];

    /// <summary>当前筛选的别名（「全部」= 不筛选）。</summary>
    [ObservableProperty]
    private string _selectedAlias = AllAliases;

    /// <summary>手动添加记录归属的别名。</summary>
    [ObservableProperty]
    private string _manualAlias = "";

    /// <summary>充值金额合计（当前别名筛选内）。</summary>
    [ObservableProperty] private string _totalDeltaText = "¥0.00";

    /// <summary>实际充值金额合计。</summary>
    [ObservableProperty] private string _totalActualText = "¥0.00";

    /// <summary>佣金合计。</summary>
    [ObservableProperty] private string _totalCommissionText = "¥0.00";

    /// <summary>差值（充值 - 实际 - 佣金）。</summary>
    [ObservableProperty] private string _diffText = "¥0.00";

    /// <summary>是否有充值记录。</summary>
    [ObservableProperty] private bool _hasRows;

    // ---- 手动添加 ----
    /// <summary>手动记录时间（预填当前时间）。</summary>
    [ObservableProperty] private string _newTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>手动充值金额。</summary>
    [ObservableProperty] private decimal _newDelta;

    /// <summary>手动实际充值金额。</summary>
    [ObservableProperty] private decimal _newActual;

    /// <summary>手动佣金。</summary>
    [ObservableProperty] private decimal _newCommission;

    /// <summary>添加失败提示。</summary>
    [ObservableProperty] private string? _addError;

    public RechargeViewModel(RechargeRepository repo)
    {
        _repo = repo;
        RefreshAliases(keepSelection: null);
        SelectedAlias = Aliases.Count > 1 ? Aliases[1] : AllAliases;
        ManualAlias = Aliases.Count > 1 ? Aliases[1] : "";
        Load();
    }

    /// <summary>筛选切换 → 手动添加别名跟随（在哪个别名视图下添加默认归哪个别名）+ 重载列表与汇总。</summary>
    partial void OnSelectedAliasChanged(string value)
    {
        if (value != AllAliases && ManualAliases.Contains(value))
            ManualAlias = value;
        Load();
    }

    /// <summary>
    /// 外部带入初始筛选（如从余额明细页选中某 Key 打开）：别名存在才切换，无效/null 保持现状。
    /// </summary>
    public void SelectAlias(string? alias)
    {
        if (alias is not null && Aliases.Contains(alias))
            SelectedAlias = alias;
    }

    /// <summary>手动添加一条充值记录（补录历史充值，归属当前手动别名）。</summary>
    [RelayCommand]
    private void Add()
    {
        if (NewDelta <= 0) { AddError = "充值金额必须大于 0"; return; }
        if (string.IsNullOrWhiteSpace(ManualAlias) || ManualAlias == AllAliases)
        {
            AddError = "请选择要归属的别名";
            return;
        }
        if (!DateTime.TryParse(NewTime, out _)) { AddError = "时间格式应为 yyyy-MM-dd HH:mm:ss"; return; }
        _repo.InsertManual(NewTime, NewDelta, NewActual, NewCommission, ManualAlias);
        AddError = null;
        NewDelta = 0; NewActual = 0; NewCommission = 0;
        NewTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Load();
    }

    /// <summary>加载：确保充值记录建行，刷新别名列表与当前筛选的明细/汇总。</summary>
    public void Load()
    {
        RefreshAliases(keepSelection: SelectedAlias);
        var rows = _repo.EnsureAndGetAll();
        var filtered = SelectedAlias == AllAliases
            ? rows
            : rows.Where(r => r.Alias == SelectedAlias).ToList();

        Rows.Clear();
        foreach (var r in filtered)
            Rows.Add(new RechargeItemVm(r));
        HasRows = Rows.Count > 0;

        var s = RechargeService.Summarize(filtered.Select(r => (r.Delta, r.Actual, r.Commission)));
        TotalDeltaText = $"¥{s.TotalDelta:F2}";
        TotalActualText = $"¥{s.TotalActual:F2}";
        TotalCommissionText = $"¥{s.TotalCommission:F2}";
        DiffText = $"¥{s.Diff:F2}";
    }

    /// <summary>刷新别名下拉（保留当前选择；选择项已不存在时回落到第一个别名）。</summary>
    private void RefreshAliases(string? keepSelection)
    {
        var keep = keepSelection ?? SelectedAlias;
        Aliases.Clear();
        Aliases.Add(AllAliases);
        foreach (var a in _repo.GetAliases())
            if (!Aliases.Contains(a)) Aliases.Add(a);

        ManualAliases.Clear();
        foreach (var a in Aliases.Skip(1))
            ManualAliases.Add(a);
        if (ManualAliases.Count > 0 && string.IsNullOrWhiteSpace(ManualAlias) || !ManualAliases.Contains(ManualAlias))
            ManualAlias = ManualAliases.FirstOrDefault() ?? "";

        if (!Aliases.Contains(keep))
            SelectedAlias = Aliases.Count > 1 ? Aliases[1] : AllAliases;
    }

    /// <summary>保存所有行的实际充值金额与佣金。</summary>
    [RelayCommand]
    private void Save()
    {
        foreach (var item in Rows)
        {
            _repo.UpdateActual(item.Row.Id, item.ActualAmount);
            _repo.UpdateCommission(item.Row.Id, item.Commission);
        }
        Load(); // 刷新汇总
    }
}

/// <summary>充值明细行 VM：实际金额可编辑（NumericUpDown 双向绑定）。</summary>
public partial class RechargeItemVm : ObservableObject
{
    public RechargeRow Row { get; }

    public string QueriedAt => Row.QueriedAt;
    public string Alias => Row.Alias;

    /// <summary>变动充值金额（余额明细 +delta）。</summary>
    public string DeltaText => $"+{Row.Delta:F2}";

    /// <summary>用户设置的实际充值金额。</summary>
    [ObservableProperty]
    private decimal _actualAmount;

    /// <summary>用户设置的佣金（充值渠道手续费等）。</summary>
    [ObservableProperty]
    private decimal _commission;

    public RechargeItemVm(RechargeRow row)
    {
        Row = row;
        _actualAmount = row.Actual;
        _commission = row.Commission;
    }
}
