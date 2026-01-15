namespace RdlCore.Abstractions.Enums;

/// <summary>
/// 表示源文档类型
/// </summary>
public enum DocumentType
{
    /// <summary>未知文档类型</summary>
    Unknown = 0,
   
    /// <summary>Microsoft Word 文档 (.docx)</summary>
    Word = 1,
    
    /// <summary>PDF 文档 (.pdf)</summary>
    Pdf = 2,
    
    /// <summary>富文本格式 (.rtf)</summary>
    Rtf = 3,
    
    /// <summary>图像文件 (.png, .jpg, .jpeg, .bmp, .tiff) - 需要 OCR</summary>
    Image = 4
}

/// <summary>
/// 表示文档中元素的逻辑角色
/// </summary>
public enum LogicalRole
{
    /// <summary>未知角色</summary>
    Unknown = 0,
    
    /// <summary>文档页眉</summary>
    Header = 1,
    
    /// <summary>文档页脚</summary>
    Footer = 2,
    
    /// <summary>文档标题</summary>
    Title = 3,
    
    /// <summary>正文内容</summary>
    Body = 4,
    
    /// <summary>表格元素</summary>
    Table = 5,
    
    /// <summary>段落文本</summary>
    Paragraph = 6,
    
    /// <summary>图像元素</summary>
    Image = 7,
    
    /// <summary>图表元素</summary>
    Chart = 8,
    
    /// <summary>页码占位符</summary>
    PageNumber = 9,
    
    /// <summary>总页数占位符</summary>
    TotalPages = 10,
    
    /// <summary>日期时间占位符</summary>
    DateTime = 11
}

/// <summary>
/// 表示 Word 文档中的域代码类型
/// </summary>
public enum FieldCodeType
{
    /// <summary>未知域类型</summary>
    Unknown = 0,
    
    /// <summary>MERGEFIELD - 数据绑定域</summary>
    MergeField = 1,
    
    /// <summary>IF - 条件域</summary>
    If = 2,
    
    /// <summary>DATE - 日期域</summary>
    Date = 3,
    
    /// <summary>PAGE - 当前页码</summary>
    Page = 4,
    
    /// <summary>NUMPAGES - 总页数</summary>
    NumPages = 5,
    
    /// <summary>TIME - 当前时间</summary>
    Time = 6,
    
    /// <summary>FORMULA - 计算公式</summary>
    Formula = 7,
    
    /// <summary>SEQ - 序列号</summary>
    Sequence = 8,
    
    /// <summary>TOC - 目录</summary>
    TableOfContents = 9,
    
    /// <summary>HYPERLINK - 超链接域</summary>
    Hyperlink = 10
}

/// <summary>
/// 表示表达式中的运算符类型
/// </summary>
public enum OperatorType
{
    /// <summary>等于 (=)</summary>
    Equals = 0,
    
    /// <summary>不等于 (not equal)</summary>
    NotEquals = 1,
    
    /// <summary>大于</summary>
    GreaterThan = 2,
    
    /// <summary>小于</summary>
    LessThan = 3,
    
    /// <summary>大于或等于</summary>
    GreaterThanOrEquals = 4,
    
    /// <summary>小于或等于</summary>
    LessThanOrEquals = 5,
    
    /// <summary>逻辑与</summary>
    And = 6,
    
    /// <summary>逻辑或</summary>
    Or = 7,
    
    /// <summary>逻辑非</summary>
    Not = 8,
    
    /// <summary>加法 (+)</summary>
    Add = 9,
    
    /// <summary>减法 (-)</summary>
    Subtract = 10,
    
    /// <summary>乘法 (*)</summary>
    Multiply = 11,
    
    /// <summary>除法 (/)</summary>
    Divide = 12,
    
    /// <summary>取模 (%)</summary>
    Modulo = 13
}

/// <summary>
/// 表示需要的干预类型
/// </summary>
public enum InterventionType
{
    /// <summary>复杂逻辑需要人工审查</summary>
    ComplexLogicReview = 0,
    
    /// <summary>布局识别模糊</summary>
    AmbiguousLayout = 1,
    
    /// <summary>OCR 置信度低</summary>
    LowOcrConfidence = 2,
    
    /// <summary>不支持的源文档功能</summary>
    UnsupportedFeature = 3,
    
    /// <summary>表达式验证失败</summary>
    ExpressionValidation = 4,
    
    /// <summary>视觉比较差异太大</summary>
    VisualMismatch = 5
}

/// <summary>
/// 表示置信度等级
/// </summary>
public enum ConfidenceLevel
{
    /// <summary>非常低的置信度</summary>
    VeryLow = 0,
    
    /// <summary>低置信度</summary>
    Low = 1,
    
    /// <summary>中等置信度</summary>
    Medium = 2,
    
    /// <summary>高置信度</summary>
    High = 3,
    
    /// <summary>非常高的置信度</summary>
    VeryHigh = 4
}

/// <summary>
/// 表示转换过程的状态
/// </summary>
public enum ConversionStatus
{
    /// <summary>未开始</summary>
    NotStarted = 0,
    
    /// <summary>进行中</summary>
    InProgress = 1,
    
    /// <summary>完成</summary>
    Completed = 2,
    
    /// <summary>完成但有警告</summary>
    CompletedWithWarnings = 3,
    
    /// <summary>失败</summary>
    Failed = 4,
    
    /// <summary>已取消</summary>
    Cancelled = 5,
    
    /// <summary>需要干预</summary>
    RequiresIntervention = 6
}

/// <summary>
/// 表示管道阶段
/// </summary>
public enum PipelinePhase
{
    /// <summary>感知阶段 - 文档分析</summary>
    Perception = 1,
    
    /// <summary>分解阶段 - 逻辑提取</summary>
    Decomposition = 2,
    
    /// <summary>综合阶段 - 模式生成</summary>
    Synthesis = 3,
    
    /// <summary>翻译阶段 - 表达式转换</summary>
    Translation = 4,
    
    /// <summary>验证阶段 - 最终验证</summary>
    Validation = 5
}

/// <summary>
/// 表示验证严重程度等级
/// </summary>
public enum ValidationSeverity
{
    /// <summary>信息消息</summary>
    Info = 0,
    
    /// <summary>警告 - 可能影响输出</summary>
    Warning = 1,
    
    /// <summary>错误 - 关键问题</summary>
    Error = 2
}

/// <summary>
/// 表示聚合函数类型
/// </summary>
public enum AggregateType
{
    /// <summary>求和</summary>
    Sum = 0,
    
    /// <summary>平均值</summary>
    Average = 1,
    
    /// <summary>计数</summary>
    Count = 2,
    
    /// <summary>最小值</summary>
    Min = 3,
    
    /// <summary>最大值</summary>
    Max = 4,
    
    /// <summary>第一个值</summary>
    First = 5,
    
    /// <summary>最后一个值</summary>
    Last = 6
}

/// <summary>
/// 表示 AST 节点类型
/// </summary>
public enum AstNodeType
{
    /// <summary>字面值</summary>
    Literal = 0,
    
    /// <summary>字段引用</summary>
    FieldReference = 1,
    
    /// <summary>参数引用</summary>
    ParameterReference = 2,
    
    /// <summary>全局变量引用</summary>
    GlobalReference = 3,
    
    /// <summary>二元操作</summary>
    BinaryOperation = 4,
    
    /// <summary>一元操作</summary>
    UnaryOperation = 5,
    
    /// <summary>函数调用</summary>
    FunctionCall = 6,
    
    /// <summary>条件表达式</summary>
    Conditional = 7,
    
    /// <summary>聚合函数</summary>
    Aggregate = 8
}
