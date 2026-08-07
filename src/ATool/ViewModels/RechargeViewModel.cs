using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ATool.Data;
using ATool.Services;

namespace ATool.ViewModels;

/// <summary>充值明细窗口 VM：自动识别余额增加记录 → 每条可设实际充值金额 → 顶部汇总充值/实际/差值。</summary>
public partial class RechargeViewModel : ObservableObject
{
    private readonly RechargeRepository _repo;

    public ObservableCollection<RechargeItemVm> Rows { get; } = [];

    /// <summary>充值金额合计（变动明细累加）。</summary>
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
        Load();
    }

    /// <summary>手动添加一条充值记录（补录历史充值）。</summary>
    [RelayCommand]
    private void Add()
    {
        if (NewDelta <= 0) { AddError = "充值金额必须大于 0"; return; }
        if (!DateTime.TryParse(NewTime, out _)) { AddError = "时间格式应为 yyyy-MM-dd HH:mm:ss"; return; }
        _repo.InsertManual(NewTime, NewDelta, NewActual, NewCommission);
        AddError = null;
        NewDelta = 0; NewActual = 0; NewCommission = 0;
        NewTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Load();
    }

    /// <summary>加载：确保充值记录建行，刷新列表与汇总。</summary>
    public void Load()
    {
        var rows = _repo.EnsureAndGetAll();
        Rows.Clear();
        foreach (var r in rows)
            Rows.Add(new RechargeItemVm(r));
        HasRows = Rows.Count > 0;

        var s = RechargeService.Summarize(rows.Select(r => (r.Delta, r.Actual, r.Commission)));
        TotalDeltaText = $"¥{s.TotalDelta:F2}";
        TotalActualText = $"¥{s.TotalActual:F2}";
        TotalCommissionText = $"¥{s.TotalCommission:F2}";
        DiffText = $"¥{s.Diff:F2}";
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
