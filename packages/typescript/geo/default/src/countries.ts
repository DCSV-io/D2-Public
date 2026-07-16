// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Side-effect import — guarantees the GeoDataInitializer coordinator
// runs (wire-nav step) before consumer code touches the catalog data.
import "./generated/geo-data-initializer.g.js";

export { Countries, CountryLookup } from "./generated/countries.g.js";
