namespace AI.Script.Syntax;

/// <summary>Вид лексемы.</summary>
public enum TokenKind
{
    /// <summary>Конец файла.</summary>
    EndOfFile,

    /// <summary>Значимый перевод строки (конец инструкции).</summary>
    Newline,

    /// <summary>Документирующий комментарий <c>#|</c>.</summary>
    DocComment,

    /// <summary>Числовой литерал.</summary>
    Number,

    /// <summary>Строковый литерал, возможно с подстановками.</summary>
    String,

    /// <summary>Литерал длительности: <c>30s</c>, <c>5m</c>.</summary>
    Duration,

    /// <summary>Литерал даты: <c>@2026-08-28</c>.</summary>
    Date,

    /// <summary>Идентификатор.</summary>
    Identifier,

    /// <summary>Плейсхолдер конвейера <c>_</c>.</summary>
    Underscore,

    // --- ключевые слова ---

    /// <summary><c>options</c></summary>
    Options,

    /// <summary><c>let</c></summary>
    Let,

    /// <summary><c>set</c></summary>
    Set,

    /// <summary><c>fn</c></summary>
    Fn,

    /// <summary><c>stage</c></summary>
    Stage,

    /// <summary><c>return</c></summary>
    Return,

    /// <summary><c>if</c></summary>
    If,

    /// <summary><c>else</c></summary>
    Else,

    /// <summary><c>for</c></summary>
    For,

    /// <summary><c>in</c></summary>
    In,

    /// <summary><c>by</c></summary>
    By,

    /// <summary><c>while</c></summary>
    While,

    /// <summary><c>break</c></summary>
    Break,

    /// <summary><c>continue</c></summary>
    Continue,

    /// <summary><c>try</c></summary>
    Try,

    /// <summary><c>catch</c></summary>
    Catch,

    /// <summary><c>use</c></summary>
    Use,

    /// <summary><c>as</c></summary>
    As,

    /// <summary><c>emit</c></summary>
    Emit,

    /// <summary><c>show</c></summary>
    Show,

    /// <summary><c>assert</c></summary>
    Assert,

    /// <summary><c>true</c></summary>
    True,

    /// <summary><c>false</c></summary>
    False,

    /// <summary><c>none</c></summary>
    None,

    /// <summary><c>nan</c></summary>
    Nan,

    /// <summary><c>inf</c></summary>
    Inf,

    // --- пунктуация и операторы ---

    /// <summary><c>(</c></summary>
    LParen,

    /// <summary><c>)</c></summary>
    RParen,

    /// <summary><c>[</c></summary>
    LBracket,

    /// <summary><c>]</c></summary>
    RBracket,

    /// <summary><c>{</c></summary>
    LBrace,

    /// <summary><c>}</c></summary>
    RBrace,

    /// <summary><c>,</c></summary>
    Comma,

    /// <summary><c>:</c></summary>
    Colon,

    /// <summary><c>.</c></summary>
    Dot,

    /// <summary><c>..</c></summary>
    DotDot,

    /// <summary><c>...</c></summary>
    Ellipsis,

    /// <summary><c>-&gt;</c></summary>
    Arrow,

    /// <summary><c>=&gt;</c></summary>
    FatArrow,

    /// <summary><c>@</c></summary>
    At,

    /// <summary><c>|&gt;</c></summary>
    Pipe,

    /// <summary><c>||</c></summary>
    OrOr,

    /// <summary><c>&amp;&amp;</c></summary>
    AndAnd,

    /// <summary><c>!</c></summary>
    Not,

    /// <summary><c>==</c></summary>
    EqEq,

    /// <summary><c>!=</c></summary>
    NotEq,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&lt;=</c></summary>
    LessEq,

    /// <summary><c>&gt;=</c></summary>
    GreaterEq,

    /// <summary><c>+</c></summary>
    Plus,

    /// <summary><c>-</c></summary>
    Minus,

    /// <summary><c>*</c></summary>
    Star,

    /// <summary><c>/</c></summary>
    Slash,

    /// <summary><c>%</c></summary>
    Percent,

    /// <summary><c>^</c></summary>
    Caret,

    /// <summary><c>=</c></summary>
    Assign,

    /// <summary><c>+=</c></summary>
    PlusAssign,

    /// <summary><c>-=</c></summary>
    MinusAssign,

    /// <summary><c>*=</c></summary>
    StarAssign,

    /// <summary><c>/=</c></summary>
    SlashAssign,
}
