<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.DataGovernance.EntityFrameworkCore - decoration paths

The ways to mark a property as anonymizable in
[`DcsvIo.D2.DataGovernance.EntityFrameworkCore`](../README.md) - the attribute, the fluent mapping helpers, and the
EF annotations each one writes.

## Attribute path - consumer-owned types

Decorate CLR properties with `[Anonymizable(...)]` and call
`builder.ApplyAnonymizationConventions()` in `ConfigureConventions`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    base.ConfigureConventions(builder);
    builder.ApplyAnonymizationConventions();
}
```

The convention walks all entity types (including EF-surfaced owned-entity types) and all
declared complex properties at finalization time. Only mapped properties are reached;
the convention skips `[NotMapped]` and unmapped members.

## Fluent path - foreign VOs and overrides

Decorate in `OnModelCreating` for types the consumer does not own (or when overriding an
attribute):

```csharp
protected override void OnModelCreating(ModelBuilder model)
{
    // Scalar on an entity
    model.Entity<User>()
         .Property(u => u.Email)
         .AnonymizeTemplate("deletedUser{UserId}@deleted.user.dcsv.io");

    // Sub-property on an owned navigation (OwnsOne)
    model.Entity<User>()
         .OwnsOne(u => u.Address, nav =>
         {
             nav.AnonymizeNull<string?>(a => a.Street);
             nav.AnonymizeEmpty<string?>(a => a.PostalCode);
         });

    // Sub-property on a complex property (selector-based - holds ComplexPropertyBuilder<T>)
    model.Entity<Profile>()
         .ComplexProperty(p => p.DisplayName, cp =>
         {
             cp.Anonymize<string>(d => d.Value, "[deleted]");
         });

    // Sub-property on a complex property (receiver-based - holds ComplexTypePropertyBuilder<T>)
    // clearedSentinel is a caller-defined tombstone, e.g. "v1." + new string('0', 64)
    model.Entity<Address>()
         .ComplexProperty(a => a.Location, cp =>
         {
             cp.Property(l => l.City).HasMaxLength(100).AnonymizeNull();
             cp.Property(l => l.PostalCode).HasMaxLength(20).AnonymizeEmpty();
             cp.Property(l => l.HashId).HasMaxLength(67).Anonymize("v1." + new string('0', 64));
         });
}
```

## Precedence

| Scenario                                  | Result                                              |
| ----------------------------------------- | --------------------------------------------------- |
| Attribute only (convention active)        | Attribute rule applied (DataAnnotation source)      |
| Fluent only                               | Fluent rule applied (Explicit source)               |
| Both (convention active + fluent)         | Fluent rule wins (Explicit > DataAnnotation)        |
| Attribute only, convention NOT registered | No annotation - property is untouched by the engine |

Divergent attribute + fluent declarations (different strategies on the same property) are not
an error at this layer - fluent wins. The startup model guard detects divergent
double-declarations.
