<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Patched RabbitMQ client maintenance

The messaging package maintainer is responsible for the rabbitmq-client distribution patch,
producer templates and provenance. The patch adds `rawBody` to delivery, get and
return messages without changing legacy decoding or confirmation scheduling.
The published D2 archive bundles the patched client and its original MIT license;
other dependencies remain ordinary registry dependencies.

## Reproduce the producer

Use the repository-pinned pnpm and Node.js versions. System `tar` must be available.
The helper runs child commands without dependency lifecycle scripts and validates
its unique scratch directory before cleanup. It never changes workspace linker
settings or installs into the source package while packing.

```bash
pnpm --filter @dcsv-io/d2-messaging-rabbitmq run pack:patched -- --refresh-producer
pnpm --filter "@dcsv-io/d2-messaging-rabbitmq..." -r --no-bail build
pnpm --filter @dcsv-io/d2-messaging-rabbitmq test -- tests/pack-patched.test.ts
pnpm --filter @dcsv-io/d2-messaging-rabbitmq run pack:patched -- /absolute/output
```

Refresh writes `packaging/producer.pnpm-lock.yaml` and
`patches/rabbitmq-client.json` from a standalone installation. Review the complete
diff. Normal packing requires the frozen producer lock and verifies the registry
integrity, patch hash, complete installed file hashes and license bytes.
The final D2 manifest retains native pnpm dependency versions and bundles only
rabbitmq-client. The helper removes source packing scripts and development pack
inputs from the published manifest. It substitutes the public Apache license for
the monorepo wrapper license inherited by native packing.

## Upgrade or retire the patch

1. Review upstream release notes and security advisories for the proposed exact pin.
2. Inspect receive assembly, message declarations and return/confirm scheduling.
3. Rebase with `pnpm patch` and `pnpm patch-commit` into this package's `patches` directory.
4. Update the exact source dependency, helper pin, producer template and workspace/export patch registrations.
5. Regenerate the root lock, producer lock and provenance with pnpm and the helper.
6. Run unit, real-broker, clean packed-consumer and raw-byte tests on Windows and Linux.
7. Verify the bundled license and include the patched client in dependency/security inventories.
8. Run canonical version and API/fingerprint reseeds after staging new inputs.
9. Record the tested version, commands, results and maintenance elapsed time in release evidence.

No fixed maintenance interval is assumed. Security updates can require immediate
work. A rebase failure or changed scheduling requires design review, not weaker
assertions. Remove the patch only when a released upstream API passes the same
raw-byte and clean-install proofs without consumer patch settings.

## Failure handling

A pack failure reports the phase and failed command and publishes no final artifact.
An existing destination is never overwritten. A source change after provenance
generation requires regeneration and review; do not hand-edit hashes to pass a check.
Inspect the recorded dependency and patch when raw-body validation fails in an
installed consumer. The runtime NACK fallback does not make an unpatched client supported.
