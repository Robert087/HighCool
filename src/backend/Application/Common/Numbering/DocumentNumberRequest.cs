namespace ERP.Application.Common.Numbering;

public sealed record DocumentNumberRequest(
    string DocumentType,
    string Prefix,
    int PaddingLength,
    int MinimumNextValue = 1);
