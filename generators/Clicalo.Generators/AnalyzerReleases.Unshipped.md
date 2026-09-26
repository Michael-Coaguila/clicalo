; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CLCC001 | Clicalo.Catalogs | Error | CatalogGenerator, InvalidJson
CLCC002 | Clicalo.Catalogs | Error | CatalogGenerator, DuplicateId
CLCC003 | Clicalo.Catalogs | Error | CatalogGenerator, NonCanonicalId
CLCC004 | Clicalo.Catalogs | Error | CatalogGenerator, NegativeValue
CLCC005 | Clicalo.Catalogs | Error | CatalogGenerator, MissingUnit
CLCC006 | Clicalo.Catalogs | Error | CatalogGenerator, KeyWithoutWin32Mapping
CLCC007 | Clicalo.Catalogs | Error | CatalogGenerator, Win32MappingForUnknownKey
CLCC008 | Clicalo.Catalogs | Error | CatalogGenerator, InvalidCodeName
CLCC009 | Clicalo.Catalogs | Error | CatalogGenerator, InvalidStructure
CLCC010 | Clicalo.Catalogs | Error | CatalogGenerator, MissingCatalog
CLCI001 | Clicalo.Localization | Error | LocalizationGenerator, InvalidJson
CLCI002 | Clicalo.Localization | Error | LocalizationGenerator, MissingTranslation
CLCI003 | Clicalo.Localization | Error | LocalizationGenerator, PlaceholderMismatch
CLCI004 | Clicalo.Localization | Error | LocalizationGenerator, UnknownPlaceholder
CLCI005 | Clicalo.Localization | Error | LocalizationGenerator, EmptyText
CLCI006 | Clicalo.Localization | Error | LocalizationGenerator, InvalidKey
CLCI007 | Clicalo.Localization | Error | LocalizationGenerator, MemberNameCollision
CLCI008 | Clicalo.Localization | Error | LocalizationGenerator, MalformedPlaceholder
CLCI009 | Clicalo.Localization | Error | LocalizationGenerator, PluralFormMissing
CLCI010 | Clicalo.Localization | Error | LocalizationGenerator, PluralFormUnexpected
CLCI011 | Clicalo.Localization | Error | LocalizationGenerator, PluralWithoutCount
CLCI012 | Clicalo.Localization | Error | LocalizationGenerator, PluralShapeMismatch
CLCI013 | Clicalo.Localization | Error | LocalizationGenerator, InvalidLocales
CLCI014 | Clicalo.Localization | Error | LocalizationGenerator, InvalidPlaceholderCatalog
CLCI015 | Clicalo.Localization | Error | LocalizationGenerator, InvalidStringsFile
CLCT001 | Clicalo.Tokens | Error | TokenGenerator, InvalidColor
CLCT002 | Clicalo.Tokens | Error | TokenGenerator, ContrastTooLow
CLCT003 | Clicalo.Tokens | Error | TokenGenerator, OutOfGamut
CLCT004 | Clicalo.Tokens | Error | TokenGenerator, MalformedFile
CLCT005 | Clicalo.Tokens | Error | TokenGenerator, UnknownToken
CLCT006 | Clicalo.Tokens | Error | TokenGenerator, StaleCorrection
CLCT007 | Clicalo.Tokens | Error | TokenGenerator, MissingFile
