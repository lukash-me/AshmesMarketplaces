# Parser testing diagnostics

This feature folder is kept for the internal `/testing` compatibility route and
manual parser diagnostics. It is not part of the user-facing navigation.

User-facing parser data is surfaced through:

- `features/parser-products` for marketplace product analytics and product
  drawer components;
- `features/orders` for logistics and demand event pages;
- `OrdersAvailabilityPage.vue` for product availability.

Do not add new seller-facing parser UI here. Prefer a dedicated feature module
or reuse `parser-products` shared components.
