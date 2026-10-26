<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.I18n

Runtime translation library. `Translator` loads the `contracts/messages/*.json` catalogs and renders `TKMessage`
instances per locale. `SupportedLocales` is a BCP 47 locale registry that reads its settings from the environment and
applies canonical casing and language fallback. The `AddD2I18n` DI extension registers both as singletons.

The types that carry no runtime behavior are in `DcsvIo.D2.I18n.Abstractions` (`TKMessage` and the `ITranslator`
interface); the `TK` constants are in `DcsvIo.D2.I18n.Keys`. Domain layers reference Abstractions. This runtime is for
infrastructure and composition-root code that renders translated strings, such as email, SMS and push notifications.

> Translation strategy: the client translates HTTP-response payloads with SvelteKit and Paraglide. The server
> translates with this `Translator` only for outbound notifications whose rendered text must be inlined before
> delivery. See the Wire format section of `DcsvIo.D2.I18n.Abstractions`.

---

## Install

```bash
dotnet add package DcsvIo.D2.I18n
```

## File layout

| Path                                 | Contents                                                                                                                                                                                                                                                                                           |
| ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Translator.cs`                      | `sealed partial class Translator : ITranslator`. Loads the JSON catalogs eagerly at construction, resolves the locale with the injected `SupportedLocales`, and falls back from the requested locale to the base locale to the raw key. `HasKey` is O(1) because it uses a pre-computed `HashSet`. |
| `SupportedLocales.cs`                | `sealed class SupportedLocales`. Reads `PUBLIC_DEFAULT_LOCALE` + `PUBLIC_ENABLED_LOCALES__N` from `IConfiguration` at construction. The instance reads its state once at construction and never changes it afterward.                                                                              |
| `I18nServiceCollectionExtensions.cs` | `services.AddD2I18n(IConfiguration, string? messagesDirectory = null)` - registers both as DI singletons. Returns `services` for chaining.                                                                                                                                                         |

---

## Public API

### `ITranslator.T(string locale, TKMessage message)`

```csharp
// Resolved via DI in a notification handler:
public sealed class SendOrgInviteEmail(ITranslator translator) : ...
{
    public async ValueTask SendAsync(User recipient, ...)
    {
        var subject = translator.T(
            recipient.PreferredLocale,
            TK.Auth.Email.INVITATION_SUBJECT.With("orgName", inviting.Name));

        var body = translator.T(
            recipient.PreferredLocale,
            TK.Auth.Email.INVITATION_BODY.With("inviter", inviter.DisplayName));
        // ...
    }
}
```

Resolution chain:

1. `SupportedLocales.Resolve(locale)` normalizes to canonical BCP 47 + applies language fallback (`fr-CH` -> `fr-FR`
   if no `fr-CH` catalog, `fr` -> `fr-FR`, etc.).
2. Lookup in the requested locale's catalog.
3. Fallback to base locale (`PUBLIC_DEFAULT_LOCALE`, default `en-US`) on miss.
4. Final fallback: return `message.Key` verbatim. **Never throws on missing keys.**

After lookup, any `{paramName}` placeholders in the template are substituted from `message.Parameters` via a source-generated regex. Unmatched placeholders are left literal.

### `SupportedLocales`

| Property           | Description                                                                                                                                                            |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Base`             | The fallback / default locale. From `PUBLIC_DEFAULT_LOCALE` or `"en-US"`.                                                                                              |
| `All`              | Every supported locale in canonical BCP 47 casing. From `PUBLIC_ENABLED_LOCALES__0..N` or `["en-US"]`.                                                                 |
| `LanguageDefaults` | Map of language prefix -> first locale of that language (e.g. `"en"` -> `"en-US"`, `"fr"` -> `"fr-FR"`). When several locales share a language, the first one is used. |

| Method                              | Description                                                                                      |
| ----------------------------------- | ------------------------------------------------------------------------------------------------ |
| `static string ToBcp47(string tag)` | Normalizes any tag to canonical casing (lowercase language, uppercase region). Pure function.    |
| `bool IsValid(string locale)`       | Whether the locale (after normalization) is in `All`.                                            |
| `string Resolve(string? locale)`    | Canonical match -> language fallback -> `Base`. Trims whitespace. Returns `Base` for null/empty. |

### `AddD2I18n`

```csharp
// Composition root in a service's Program.cs:
builder.Services.AddD2I18n(builder.Configuration);
```

Registers both `SupportedLocales` and `ITranslator` as singletons with `TryAddSingleton`, so calling it twice is safe.
The catalog directory of the `Translator` defaults to `Path.Combine(AppContext.BaseDirectory, "messages")`. The
consuming csproj fills that directory at build time with
`<Content Include="...contracts/messages/*.json" CopyToOutputDirectory="PreserveNewest" />`. The default works for any
service that follows the convention; pass an explicit `messagesDirectory` for tests or non-standard layouts.

---

## Configuration

| Env var                                                           | Purpose                                                                                                      | Default     |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ----------- |
| `PUBLIC_DEFAULT_LOCALE`                                           | The base locale (fallback when key missing in requested locale, and when `Resolve` gets a null/empty input). | `en-US`     |
| `PUBLIC_ENABLED_LOCALES__0`<br>`PUBLIC_ENABLED_LOCALES__1`<br>... | Indexed list of supported locales. Empty/whitespace entries skipped.                                         | `["en-US"]` |

The indexed environment variables follow the `IConfiguration` indexed-section binding. `SupportedLocales` normalizes
casing to canonical BCP 47 when it loads, so `EN-us` and `en-US` produce the same canonical entry.

---

## Catalog files

The `Translator` loads any `*.json` file in its messages directory; each filename (without extension) is treated as the locale. The expected shape is a flat object of string-to-string:

```json
{
  "$schema": "./schema.json",
  "common_errors_NOT_FOUND": "Not found.",
  "common_errors_FORBIDDEN": "Forbidden.",
  "auth_email_invitation_subject": "{inviter} invited you to {orgName}"
}
```

The `Translator` drops the `$schema` key when it loads a catalog. Parameter substitution uses `{paramName}`
placeholders, which are matched against `TKMessage.Parameters` at render time.

The source catalogs are in `contracts/messages/`. The consuming csproj copies them to the output directory with:

```xml
<ItemGroup>
  <Content Include="..\..\..\..\..\contracts\messages\*.json"
           LinkBase="messages"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## Dependencies

- `DcsvIo.D2.I18n.Abstractions` - `TKMessage` and `ITranslator`
- `DcsvIo.D2.Utilities` - `Falsey()` for boundary checks while parsing configuration
- `Microsoft.Extensions.Configuration.Abstractions` + `Microsoft.Extensions.Configuration.Binder` - `IConfiguration` for env-var ingestion
- `Microsoft.Extensions.DependencyInjection.Abstractions` - `IServiceCollection` for the `AddD2I18n` extension

The package has no `Microsoft.AspNetCore.App` framework reference, so it does not depend on the HTTP stack and a
console worker or a Hangfire job can use it.

---

## Tests

The `DcsvIo.D2.Tests` I18n unit suite has these test classes:

- `SupportedLocalesTests` - ToBcp47 normalization, configuration ingestion (defaults, mixed-case, empty entries, dedup
  behavior), language-default ordering, `IsValid` / `Resolve` adversarial coverage.
- `TranslatorTests` - construction validation (null / missing / malformed dir), basic lookup, base-locale fallback,
  language-prefix resolution, parameter substitution (single, multiple, missing param, extra param, placeholder twice,
  brace-in-value non-recursion), `HasKey`, 100-caller concurrency stress.
- `I18nServiceCollectionExtensionsTests` - singleton registration, `IConfiguration` propagation, `TryAdd` idempotency, fluent return.
- `TKGeneratedTests` - end-to-end smoke test that the source generator and Abstractions together produce `TK` constants
  matching the live `en-US.json`.

The tests cover every translator path, every supported-locale operation and every DI registration branch.

---

## Reference

- `DcsvIo.D2.I18n.Abstractions` - `TKMessage` and the wire format
- `DcsvIo.D2.I18n.Keys` - the `TK` constants and their source generator
- `DcsvIo.D2.Result` - `D2Result.Messages` and `InputError.Errors` use `TKMessage`
- `contracts/messages/` - the source catalogs
