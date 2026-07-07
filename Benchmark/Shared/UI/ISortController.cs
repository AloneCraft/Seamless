using System.ComponentModel;
using System.Text.Json;

namespace UI;

// 縺薙％繧堤ｷｨ髮・☆繧句ｴ蜷・
// - 繧ｽ繝ｼ繝医Ν繝ｼ繝ｫ縺ｮ逕滓・/譖ｴ譁ｰ/菫晏ｭ倬・ｺ丞､画峩繝ｫ繝ｼ繝ｫ
// - 菫晏ｭ俶ｸ医∩險ｭ螳夂ｴ謳肴凾縺ｮ蠕ｩ蜈・Ν繝ｼ繝ｫ
internal interface ISortController {
    void Initialize();
    void SortBenchmarkRows(GridSortColumn column);
    void AddBenchmarkSortRule();
    void RemoveBenchmarkSortRule(SortRule rule);
    void MoveBenchmarkSortRule(SortRule source,SortRule target);
    void MoveBenchmarkSortRuleUp(SortRule rule);
    void MoveBenchmarkSortRuleDown(SortRule rule);
    void SetBenchmarkSortRuleColumn(SortRule rule,GridSortColumn column);
    void ToggleBenchmarkSortRuleDirection(SortRule rule);
    void ResetBenchmarkSortRules();
    void RefreshBenchmarkSortColumns();
    void RefreshBenchmarkSortColumnOptions();
    void ApplyCurrentSort();
    void ApplyFinalBenchmarkSort();
}

