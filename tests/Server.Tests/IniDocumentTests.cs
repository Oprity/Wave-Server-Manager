// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using AcServerManager.Ini;

namespace AcServerManager.Tests;

public sealed class IniDocumentTests
{
    [Fact]
    public void Parse_Serialize_WithoutEdits_IsByteIdentical()
    {
        var text =
            "; top comment\n" +
            "\n" +
            "[SERVER]\n" +
            "NAME=My Server ; inline note\n" +
            "CARS=car_a,car_b\n" +
            "CUSTOM_UNKNOWN=keep me\n" +
            "\n" +
            "# hash comment\n" +
            "[PRACTICE]\n" +
            "TIME=10\n";

        var doc = IniDocument.Parse(text);
        Assert.Equal(text, doc.Serialize());
    }

    [Fact]
    public void SetValue_Preserves_UnknownKeys_Comments_And_TrailingComment()
    {
        var text =
            "; managed by tool - do not remove\n" +
            "[SERVER]\n" +
            "NAME=Old ; keep this note\n" +
            "CARS=a,b\n" +
            "SOME_FUTURE_KEY=42\n";

        var doc = IniDocument.Parse(text);
        doc.SetValue("SERVER", "NAME", "New");

        var output = doc.Serialize();
        Assert.Contains("NAME=New ; keep this note\n", output);
        Assert.Contains("SOME_FUTURE_KEY=42\n", output);
        Assert.Contains("; managed by tool - do not remove\n", output);
    }

    [Fact]
    public void SetValue_Appends_MissingSection_And_Key()
    {
        var doc = IniDocument.Parse("[SERVER]\nNAME=x\n");
        doc.SetValue("RACE", "LAPS", "10");

        Assert.Equal("10", doc.GetValue("RACE", "LAPS"));
        var output = doc.Serialize();
        Assert.Contains("[RACE]\n", output);
        Assert.Contains("LAPS=10\n", output);
    }

    [Fact]
    public void Lookup_Is_CaseInsensitive()
    {
        var doc = IniDocument.Parse("[server]\nname=Lower\n");
        Assert.Equal("Lower", doc.GetValue("SERVER", "NAME"));
        doc.SetValue("Server", "Name", "Upper");
        Assert.Equal("Upper", doc.GetValue("SERVER", "NAME"));
    }

    [Fact]
    public void Password_With_Semicolon_Is_Not_Treated_As_Comment()
    {
        var doc = IniDocument.Parse("[SERVER]\nPASSWORD=p;ss\n");
        Assert.Equal("p;ss", doc.GetValue("SERVER", "PASSWORD"));
    }

    [Fact]
    public void EntryList_MultipleCarSections_Survive()
    {
        var text = "[CAR_0]\nMODEL=car_a\nSKIN=red\n\n[CAR_1]\nMODEL=car_b\nSKIN=\n";
        var doc = IniDocument.Parse(text);
        doc.SetValue("CAR_0", "SKIN", "blue");
        var output = doc.Serialize();
        Assert.Contains("MODEL=car_a\n", output);
        Assert.Contains("SKIN=blue\n", output);
        Assert.Contains("[CAR_1]\n", output);
    }
}
