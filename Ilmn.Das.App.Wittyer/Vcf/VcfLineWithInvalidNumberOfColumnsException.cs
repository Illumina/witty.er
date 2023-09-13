namespace Ilmn.Das.App.Wittyer.Vcf;

using System;
using System.Collections.Generic;

internal class VcfLineWithInvalidNumberOfColumnsException : Exception
{
    private readonly int _actualNumberOfColumns;
    private readonly int _expectedNumberOfColumns;
    private readonly IReadOnlyList<string> _invalidSplitLine;
    private readonly string _invalidVcfLine;

    public VcfLineWithInvalidNumberOfColumnsException(
        string invalidVcfLine,
        IReadOnlyList<string> invalidSplitLine,
        int expectedNumberOfColumns,
        int actualNumberOfColumns)
        : this(invalidVcfLine, invalidSplitLine, expectedNumberOfColumns, actualNumberOfColumns, CreateMessage(invalidVcfLine, expectedNumberOfColumns, actualNumberOfColumns))
    {
    }

    public VcfLineWithInvalidNumberOfColumnsException(
        string invalidVcfLine,
        IReadOnlyList<string> invalidSplitLine,
        int expectedNumberOfColumns,
        int actualNumberOfColumns,
        string message,
        Exception? inner = null)
        : base(message, inner)
    {
        _expectedNumberOfColumns = expectedNumberOfColumns;
        _actualNumberOfColumns = actualNumberOfColumns;
        _invalidSplitLine = invalidSplitLine;
        _invalidVcfLine = invalidVcfLine;
    }

    private static string CreateMessage(string invalidVcfLine, int expectedNumberOfColumns, int actualNumberOfColumns)
        => $"Invalid number of columns (expected: {expectedNumberOfColumns}, actual: {actualNumberOfColumns}) in the following VCF line:\n{invalidVcfLine}";
}