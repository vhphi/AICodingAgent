# Example Requirement

## Title
Customer Search

## Description
Implement a customer search feature.

## Functional requirements
- Search by customer code.
- Search by customer name.
- Search is case-insensitive.
- Inactive customers must not be returned.
- Support page number and page size.

## Acceptance criteria
- Given an active customer, searching by exact code returns the customer.
- Given a customer name, a partial case-insensitive search returns matching active customers.
- Inactive customers are excluded.
- Pagination is deterministic.
