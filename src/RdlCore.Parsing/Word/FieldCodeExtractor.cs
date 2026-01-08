using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AxiomFieldCode = RdlCore.Abstractions.Models.FieldCode;
using OpenXmlFieldCode = DocumentFormat.OpenXml.Wordprocessing.FieldCode;

namespace RdlCore.Parsing.Word;

/// <summary>
/// Extracts field codes from Word documents
/// </summary>
public partial class FieldCodeExtractor
{
    private readonly ILogger<FieldCodeExtractor> _logger;
    private int _fieldIdCounter;

    public FieldCodeExtractor(ILogger<FieldCodeExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts all field codes from a Word document
    /// </summary>
    public IReadOnlyList<AxiomFieldCode> ExtractFieldCodes(WordprocessingDocument document)
    {
        var fieldCodes = new List<AxiomFieldCode>();
        _fieldIdCounter = 0;

        var mainPart = document.MainDocumentPart;
        if (mainPart?.Document?.Body == null)
        {
            return fieldCodes;
        }

        // Extract from main body
        ExtractFromElement(mainPart.Document.Body, fieldCodes);

        // Extract from headers
        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header != null)
            {
                ExtractFromElement(headerPart.Header, fieldCodes);
            }
        }

        // Extract from footers
        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer != null)
            {
                ExtractFromElement(footerPart.Footer, fieldCodes);
            }
        }

        _logger.LogInformation("Extracted {Count} field codes from document", fieldCodes.Count);
        return fieldCodes;
    }

    private void ExtractFromElement(OpenXmlElement element, List<AxiomFieldCode> fieldCodes)
    {
        // Find simple fields (w:fldSimple)
        foreach (var simpleField in element.Descendants<SimpleField>())
        {
            var fieldCode = ParseSimpleField(simpleField);
            if (fieldCode != null)
            {
                fieldCodes.Add(fieldCode);
            }
        }

        // Find complex fields (w:fldChar)
        var complexFields = ExtractComplexFields(element);
        fieldCodes.AddRange(complexFields);
    }

    private AxiomFieldCode? ParseSimpleField(SimpleField simpleField)
    {
        var instruction = simpleField.Instruction?.Value;
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return null;
        }

        return ParseFieldInstruction(instruction.Trim());
    }

    private IEnumerable<AxiomFieldCode> ExtractComplexFields(OpenXmlElement element)
    {
        var fieldCodes = new List<AxiomFieldCode>();
        var fieldChars = element.Descendants<FieldChar>().ToList();
        
        var fieldStack = new Stack<(int start, StringBuilder instruction)>();
        var currentInstruction = new StringBuilder();
        var inField = false;

        foreach (var para in element.Descendants<Paragraph>())
        {
            foreach (var run in para.Descendants<Run>())
            {
                var fieldChar = run.GetFirstChild<FieldChar>();
                
                if (fieldChar != null)
                {
                    var fieldCharType = fieldChar.FieldCharType?.Value;
                    
                    if (fieldCharType == FieldCharValues.Begin)
                    {
                        if (inField)
                        {
                            // Nested field
                            fieldStack.Push((fieldCodes.Count, currentInstruction));
                            currentInstruction = new StringBuilder();
                        }
                        inField = true;
                    }
                    else if (fieldCharType == FieldCharValues.Separate)
                    {
                        // Field result starts here, we have the full instruction
                    }
                    else if (fieldCharType == FieldCharValues.End)
                    {
                        if (inField)
                        {
                            var instruction = currentInstruction.ToString().Trim();
                            var fieldCode = ParseFieldInstruction(instruction);
                            if (fieldCode != null)
                            {
                                fieldCodes.Add(fieldCode);
                            }

                            if (fieldStack.Count > 0)
                            {
                                var (_, parentInstruction) = fieldStack.Pop();
                                currentInstruction = parentInstruction;
                            }
                            else
                            {
                                    currentInstruction.Clear();
                                    inField = false;
                                }
                            }
                            break;
                    }
                }
                else if (inField)
                {
                    // Collect field code text
                    var fieldCode = run.GetFirstChild<OpenXmlFieldCode>();
                    if (fieldCode != null)
                    {
                        currentInstruction.Append(fieldCode.Text);
                    }
                }
            }
        }

        return fieldCodes;
    }

    private AxiomFieldCode? ParseFieldInstruction(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return null;
        }

        var id = $"field_{++_fieldIdCounter}";
        var type = DetermineFieldType(instruction);
        var fieldName = ExtractFieldName(instruction, type);
        var switches = ExtractSwitches(instruction);

        _logger.LogDebug("Parsed field: Type={Type}, Name={Name}, Instruction={Instruction}", 
            type, fieldName, instruction);

        return new AxiomFieldCode(
            id,
            type,
            instruction,
            fieldName,
            switches.Count > 0 ? switches : null,
            null);
    }

    private static FieldCodeType DetermineFieldType(string instruction)
    {
        var upperInstruction = instruction.ToUpperInvariant().TrimStart();

        if (upperInstruction.StartsWith("MERGEFIELD")) return FieldCodeType.MergeField;
        if (upperInstruction.StartsWith("IF")) return FieldCodeType.If;
        if (upperInstruction.StartsWith("DATE")) return FieldCodeType.Date;
        if (upperInstruction.StartsWith("TIME")) return FieldCodeType.Time;
        if (upperInstruction.StartsWith("PAGE")) return FieldCodeType.Page;
        if (upperInstruction.StartsWith("NUMPAGES")) return FieldCodeType.NumPages;
        if (upperInstruction.StartsWith("=") || upperInstruction.StartsWith("FORMULA")) return FieldCodeType.Formula;
        if (upperInstruction.StartsWith("SEQ")) return FieldCodeType.Sequence;
        if (upperInstruction.StartsWith("TOC")) return FieldCodeType.TableOfContents;
        if (upperInstruction.StartsWith("HYPERLINK")) return FieldCodeType.Hyperlink;

        return FieldCodeType.Unknown;
    }

    private string? ExtractFieldName(string instruction, FieldCodeType type)
    {
        return type switch
        {
            FieldCodeType.MergeField => ExtractMergeFieldName(instruction),
            FieldCodeType.If => null, // IF fields don't have a simple name
            _ => null
        };
    }

    private static string? ExtractMergeFieldName(string instruction)
    {
        var match = MergeFieldRegex().Match(instruction);
        return match.Success ? match.Groups[1].Value : null;
    }

    private Dictionary<string, string> ExtractSwitches(string instruction)
    {
        var switches = new Dictionary<string, string>();

        // Match switches like \* MERGEFORMAT, \# "0.00", \@ "yyyy-MM-dd"
        var matches = SwitchRegex().Matches(instruction);
        
        foreach (Match match in matches)
        {
            var switchName = match.Groups[1].Value;
            var switchValue = match.Groups[2].Success 
                ? match.Groups[2].Value.Trim('"', '\'') 
                : string.Empty;
            
            switches[switchName] = switchValue;
        }

        return switches;
    }

    [GeneratedRegex(@"MERGEFIELD\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex MergeFieldRegex();

    [GeneratedRegex(@"\\([#@*!])\s*(?:""([^""]*)""|(\S+))?")]
    private static partial Regex SwitchRegex();
}
