# <img src="https://raw.githubusercontent.com/maxkagamine/Kagamine.Extensions/bf0cdc7eea084e4bb7fbee740147242df87b975c/icon.svg" width="38" height="38" alt="🍊️" align="top" />&nbsp;Kagamine.Extensions

[![Kagamine.Extensions](https://img.shields.io/nuget/v/Kagamine.Extensions?logo=nuget&label=Kagamine.Extensions)](https://www.nuget.org/packages/Kagamine.Extensions) [![Kagamine.Extensions.EntityFramework](https://img.shields.io/nuget/v/Kagamine.Extensions.EntityFramework?logo=nuget&label=Kagamine.Extensions.EntityFramework)](https://www.nuget.org/packages/Kagamine.Extensions.EntityFramework)

[English](README.md)

このリポジトリには、本番に適したアプリケーションを開発する際に一般的に必要となるファシリティを提供するライブラリ群が含まれています（Microsoftの言い方を借りると）。私のすべての作品と同じく人間の手で書かれたコードです。

- [Hosting](#hosting)
  - [ConsoleApplication.CreateBuilder()](#consoleapplicationcreatebuilder)
- [Collections](#collections)
  - [ValueArray\<T\>](#valuearrayt)
- [IO](#io)
  - [TemporaryFileProvider](#temporaryfileprovider)
- [Http](#http)
  - [RateLimitingHttpHandler](#ratelimitinghttphandler)
- [Logging](#logging)
  - [BeginTimedOperation](#begintimedoperation)
- [Utilities](#utilities)
  - [TerminalProgressBar](#terminalprogressbar)
- [EntityFramework](#entityframework)
  - [Update\<T\>(this DbSet\<T\> set, T entity, T valuesFrom)](#updatetthis-dbsett-set-t-entity-t-valuesfrom)
  - [ToHashSetAsync\<T\>()](#tohashsetasynct)

## Hosting

### ConsoleApplication.CreateBuilder()

WebApplicationがASP.NET Coreに対して行うように、[汎用ホスト](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/generic-host)のフレームワークをコンソールアプリに合わせるものです。IHostの使用は、依存関係の挿入・ログ記録・構成のシステム、およびウェブアプリとの一貫性のために望ましい（それにEF Coreの移行はDbContextを発見するためにそれを使う）ですが、初期ユーザー体験は主にバックグラウンドワーカー向けに設計されたので、普通の実行可能プログラムのために使おうとすると色々不満につながります。

Program.csの例：

```cs
using Kagamine.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = ConsoleApplication.CreateBuilder();

builder.Services.AddDbContext<FooContext>();
builder.Services.AddScoped<IFooService, FooService>();

// asyncであり、および/または終了コードを返すことも可能です
builder.Run((IFooService fooService, CancellationToken cancellationToken) =>
{
    fooService.DoStuff(cancellationToken);
});
```

コンソールアプリを実行するようにIHostedServiceかBackgroundServiceを転用することに比べて、

- エントリーポイントははるかにクリーンで自然です（Minimal APIを連想させます）
- `SIGINT`、`SIGQUIT`、`SIGTERM`は正しい終了コードを発生させます（バックグラウンドのサービスでは、Ctrl+Cは「正常な」シャットダウンをトリガーして、成功ステータス 0 で終了する仕様ですが、これは長期間稼働するワーカーやサーバーでのみ理にかなっています）
- 未処理の例外は普通のコンソールアプリと同じで、代わりにILogger経由になるだけです（開発者向けのメッセージを添えて2回出力されることがありません。ログがフラッシュされるように、サービスも破棄されます）
- アプリケーションの有効期間イベントは正しく取り扱われます（実はこれを間違えているIHostedServiceの例は多いです。ちゃんと実装するのが意外と直感的じゃないし、そもそもこの目的のために作られたものではありません）

これを実際に使っている実例は、[Serifu.orgのプロジェクトで](https://github.com/maxkagamine/Serifu.org/blob/master/Serifu.Importer.Skyrim/Program.cs)いくつかあります。

> [!NOTE]
> ASP.NET Coreのプロジェクトは、開発で環境を「Development」に設定するlaunchSettings.jsonをデフォルトで含めますが、コンソールアプリのためには[このファイルを自分で作る](https://learn.microsoft.com/ja-jp/aspnet/core/fundamentals/environments)必要があります。最も簡単な方法は、Visual Studioでデバッグ → {プロジェクト名} のデバッグ プロパティを開いて、環境変数の下に`DOTNET_ENVIRONMENT` = `Development`を追加することです。`ASPNETCORE_`プレフィックスはWebApplicationに固有なので、ここで使えないことに注意してください。

## Collections

### ValueArray&lt;T&gt;

現在、.NETでは不変性と値セマンティクスの両方を維持しながらコレクションをレコードに入れる解決策は存在しません。それにスパンに対応しないAPIとの相互運用のために、基になる配列へのアクセスが必要になる場合もあります（特に、コピーするとパフォーマンスに大きな影響を与える可能性のあるバイト配列の場合）。

これを解決するため、私は不変のレコードでの使用に適した値セマンティクスのある読み込み専用配列を表すValueArray&lt;T&gt;型を作りました。

| 型                          | 不変              | 値の等価性      | コピーせず配列へ/から |
| --------------------------- | ---------------- | -------------- | ------------------ |
| T[]                         | ❌               | ❌            | ✅                 |
| List&lt;T&gt;               | ❌               | ❌            | ❌                 |
| ReadOnlyCollection&lt;T&gt; | ✅<sup>1</sup>   | ❌            | ❌                 |
| IReadOnlyList&lt;T&gt;      | ✅<sup>1</sup>   | ❌            | ❌                 |
| ImmutableArray&lt;T&gt;<sup>2</sup> | ✅<sup>3,4</sup> | ❌    | ✅<sup>3</sup>     |
| ReadOnlyMemory&lt;T&gt;     | ✅<sup>4,5</sup> | ❌            | ⚠<sup>5</sup>     |
| **ValueArray&lt;T&gt;**     | ✅<sup>4,6</sup> | ✅            | ✅<sup>6</sup>     |

> 1. ReadOnlyCollection&lt;T&gt;はただのList&lt;T&gt;の読み込み専用ビューで、IReadOnlyList&lt;T&gt;は普通にList&lt;T&gt;自体です。
> 2. null免除演算子の誤用によるバグがあります。いずれかのコードがその`default`を返すと、静的解析で検出されないnull参照例外が発生する可能性があります。（ValueArray&lt;T&gt;も構造体ですが、null配列を空の配列として扱ってこの問題を回避します。）
> 3.  [ImmutableCollectionsMarshal](https://learn.microsoft.com/ja-jp/dotnet/api/system.runtime.interopservices.immutablecollectionsmarshal)を使って、基になる配列にアクセスすることも、既存の配列を基にしたインスタンスを作成することも可能です。
> 4. 構築するために使用した配列への参照が保持される場合、もしくは基になるバッファがアクセスされて読み込み専用として扱わないメソッドへ渡される場合は、誤って変更される可能性があります。
> 5. ReadOnlyMemory&lt;T&gt;がどうやって作られたかによって、[MemoryMarshal](https://learn.microsoft.com/ja-jp/dotnet/api/system.runtime.interopservices.memorymarshal.trygetarray)を使ってバッファにアクセスすることが可能かもしれませんが、インスタンスが実際の配列に裏付けられている保証がないし、配列のスライスを表す可能性もあります（Span&lt;T&gt;みたいに）。
> 6. T[]からの暗黙的な変換をサポートし、基になる配列はT[]への明示的なキャストによってアクセスできます。

ValueArray&lt;T&gt;は[コレクション式](https://learn.microsoft.com/ja-jp/dotnet/csharp/language-reference/operators/collection-expressions)も、配列初期化子も（暗黙的な変換により）サポートします：

```cs
record Song(string Title, ValueArray<string> Artists);

Song song = new("Promise", ["samfree", "鏡音リン", "初音ミク"]);
Song song2 = song with { Artists = [.. song.Artists] /* 配列をクローン */ };

// これらは、ArtistsがList<T>だったら、内容が同一なのに失敗します
Assert.True(song == song2);
Assert.True(song.Artists == song2.Artists);

ValueArray<Song> songs = new[] { song, song2 };
```

スパンを使うAPIと、Entity Frameworkのように配列を必要とするAPIと相互運用可能です。値コンバーターを使って、ValueArray&lt;byte&gt;は基になるbyte[]にキャストして、配列をコピーするオーバーヘッドなしでBLOB列に使えます：

```cs
entity.Property<ValueArray<byte>>(x => x.Data)
    .HasColumnName("data")
    .HasConversion(model => (byte[])model, column => column);
```

`T`が[アンマネージ型](https://learn.microsoft.com/ja-jp/dotnet/csharp/language-reference/builtin-types/unmanaged-types)である場合は、ValueArray&lt;T&gt;がReadOnlySpan&lt;byte&gt;へ/からマーシャリングすることもできます。例えば、構造体の配列をバイナリ表現のままにして、不透明なblobとしてデータベースに保存する用途に使えます。

この機能を活用して、ValueArray&lt;T&gt;を効率的にbase64の文字列としてシリアル化するJsonConverterを作成しました：

```cs
ValueArray<DateTime> dates = [ DateTime.Parse("2007-08-31"), DateTime.Parse("2007-12-27") ];

var options = new JsonSerializerOptions() { Converters = { new JsonBase64ValueArrayConverter() } };
var json = JsonSerializer.Serialize(dates, options); // "AIAeAnm5yQgAAN2OMhbKCA=="
```

JSON配列をValueArray&lt;T&gt;として逆シリアル化するには（System.Text.Jsonはネイティブにカスタムな読み込み専用コレクションに逆シリアル化できないため）、JsonValueArrayConverterを使用してください。両方のコンバーターは、特定のTのために組み合わせられるジェネリック版もあります。

## IO

### TemporaryFileProvider

`Path.GetTempFileName()`よりも一時ファイルを扱うために多くの利点を提供します：

- `GetTempFileName()`と違って、ファイル拡張子かサフィックスを指定できます（あるプログラムにファイルパスを渡す際に必要だし、Stack Overflowにある一般的な解決策と違ってファイル名の一意性を保証し、競合状態を回避します）
- 一時ファイルはアプリケーション固有のディレクトリに保存され、終了時にディレクトリが空であれば削除されます
- `using`に入れると、TemporaryFileが破棄されたら一時ファイルが自動的に片付けられます
- TemporaryFileはファイルへのオープンハンドルを保持しないので、ファイルを上書きしたり置き換えたりする可能性がある（したがって使用中でないことを期待する）ffmpegなどのプログラムにファイルパスを渡すことができます
- 👉 <b>最も重要なのは、</b>TemporaryFileは参照カウントを保持して、自身とすべてのストリームが破棄された後にのみファイルを削除するので、メソッドが後始末を気にせずに一時ファイルに裏付けられているFileStreamを返せます。これにより、以下のような一般的なエラー処理パターンが大幅に簡素化されます：

```cs
public async Task<Stream> ConvertToOpus(Stream inputStream, CancellationToken cancellationToken)
{
    using TemporaryFile inputFile = tempFileProvider.Create();
    await inputFile.CopyFromAsync(inputStream);

    using TemporaryFile outputFile = tempFileProvider.Create(".opus");

    await FFMpegArguments
        .FromFileInput(inputFile.Path)
        .OutputToFile(outputFile.Path, overwrite: true, options => options
            .WithAudioBitrate(Bitrate))
        .CancellableThrough(cancellationToken)
        .ProcessAsynchronously();

    // ffmpegがスローする場合、両方の一時ファイルが削除されます。
    //
    // 成功する場合、入力ファイルは削除され、出力ファイルは返されたストリームが閉じるまでに
    // ディスク上に残って、その時点でその残っている一時ファイルが自動的に片付けられます。
    return outputFile.OpenRead();
}
```

ITemporaryFileProviderはこのようにサービスコンテナに追加されます：

```cs
services.AddTemporaryFileProvider();
```

または、DIを使っていない場合は、自分でTemporaryFileProviderを構築できます。一時ディレクトリとベースファイル名の形式（デフォルトでGuid）はオプションで変更可能です（オーバーロードを参照してください）。

## Http

### RateLimitingHttpHandler

[System.Threading.RateLimiting](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/)を使って、同じホストへのリクエストを前のリクエストが完了してから新しいリクエストを送信する前に設定された時間を待たせるDelegatingHandlerです。

```cs
// すべてのHttpClientにレートリミッターを追加
builder.Services.ConfigureHttpClientDefaults(builder => builder.AddRateLimiting());

// ライブラリでは、自分の名前付きか型指定されたクライアントにのみ追加することを推奨します。
// トップレベルのプロジェクトがすべてのクライアントに追加しても、レートリミットは重複しません。
builder.Services.AddHttpClient("foo").AddRateLimiting();

// 代わりに、DIを使っていない場合
using RateLimitingHttpHandlerFactory rateLimiterFactory = new();
RateLimitingHttpHandler rateLimiter = rateLimiterFactory.CreateHandler();
rateLimiter.InnerHandler = new HttpClientHandler();
HttpClient client = new(rateLimiter);
```

DIを使っている場合は、ホストごとのレートリミットはすべての名前付きクライアントに共有されます。これは、コードがたまたま複数のクライアントを使用しているという理由だけで意図よりも頻繁にホストを誤って叩くことを避けます。

デフォルトのリクエスト間の時間を変更して、またはホストごとに異なるレートリミットを設定するには：

```cs
builder.Services.Configure<RateLimitingHttpHandlerOptions>(options =>
{
    // nullに設定すると、デフォルトでレートリミットが無効になります。デフォルトで有効のまま
    // にして、代わりに特定のホストに対して無効にすることもできます。自分のAPIに特定のレート
    // リミットを実施する必要があるライブラリは、グローバルのTimeBetweenRequestsに依存す
    // べきではありません。
    options.TimeBetweenRequests = null;
    options.TimeBetweenRequestsByHost.Add("example.com", TimeSpan.FromSeconds(5));
});
```

タイマーは、リクエストを送信する前ではなく、レスポンスが受信されて呼び出し元に返された後で開始することに注意してください。じゃないと、遅いレスポンスやネットワーク遅延によってリクエストがレートリミットを実質的に受けていない挙動を示す可能性があります。

デモを見るには、サンプル[ConsoleApp](Samples/ConsoleApp/Program.cs)を実行してください。

## Logging

### BeginTimedOperation

私が以前多くのプロジェクトで使ったSerilogMetricsに触発された小さな拡張メソッドです：

```cs
using (logger.BeginTimedOperation(nameof(DoStuff)))
{
    logger.Debug("何かやってる...");
}
// [12:00:00 INF] DoStuff: Starting
// [12:00:00 DBG] 何かやってる...
// [12:00:01 INF] DoStuff: Completed in 39 ms
```

## Utilities

### TerminalProgressBar

[ターミナルでプログレスバーを表示する](https://learn.microsoft.com/ja-jp/windows/terminal/tutorials/progress-bar-sequences)ためにANSIエスケープコードを送信して、破棄されたら自動的にクリアします：

```cs
using var progress = new TerminalProgressBar();

for (int i = 0; i < foos.Count; i++)
{
    logger.Information("Foo {Foo} of {TotalFoos}", i + 1, foos.Count);
    progress.SetProgress(i, foos.Count);

    await fooService.DoStuff(foos[i]);
}
```

## EntityFramework

### Update&lt;T&gt;(this DbSet&lt;T&gt; set, T entity, T valuesFrom)

既存のエンティティを新しいインスタンスに置き換えることを可能にして、通常のプロパティ値とナビゲーションプロパティの両方を正しく移します。普段は、同じ主キーを持つ他のインスタンスがトラッキング中で（例えば別の場所で行われた別のクエリにより）接続解除エンティティをUpdate()に渡そうとするとEFがスローします。

```cs
var existingEntities = await db.Foos.ToDictionaryAsync(f => f.Id);

foreach (var entity in entities)
{
    if (existingEntities.Remove(entity.Id, out var existingEntity))
    {
        db.Foos.Update(existingEntity, entity);
    }
    else
    {
        db.Foos.Add(entity);
    }
}

db.Foos.RemoveRange(existingEntities.Values);
await db.SaveChangesAsync();
```

> [!TIP]
> アプリケーションにとって理にかなっているならば、代わりにIDbContextFactoryを使用して、各Unit of Workのために新しいコンテキストを作ることをご検討ください。そうすると、このようなことを必要とする「[不気味な遠隔作用（spooky action at a distance）](https://en.wikipedia.org/wiki/Action_at_a_distance_(computer_programming))」のようなこと（コードの他の部分が予測不能に変更トラッカーの状態に影響を与えること）を回避できます。

### ToHashSetAsync&lt;T&gt;()

⚠ **EF 9で正式になったので、v2.0.0で削除されました。より古いプロジェクトは、メソッドを[ここから](https://github.com/maxkagamine/Kagamine.Extensions/blob/v1.10.2/Kagamine.Extensions.EntityFramework/EntityFrameworkExtensions.cs)コピーできます。**

ToArrayAsyncとToListAsyncと同様。その2つと同じく`await foreach`を使って実装されたので、ToListAsyncの次にToHashSetを呼び出すより少し効率的です：

```cs
HashSet<string> referencedFiles = await db.Foos
    .Select(f => f.FilePath)
    .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

foreach (var file in Directory.EnumerateFiles(dir))
{
    if (!referencedFiles.Contains(file))
    {
        logger.Warning("孤立ファイル {Path} を削除しています", file);
        File.Delete(file);
    }
}
```
