# Contracts 0.4.2 live acceptance

Use the installed 0.4.2 DLL. At startup, expect a migration line on a legacy install followed by:

`Gold and direct personal XP rewards enabled outside raids`

Do not accept `XP disabled by config` unless XP was deliberately turned off after schema 1 was written.

1. Open the Contracts board and confirm offers show `Reward | +X Gold +Y XP` before acceptance.
2. Record current XP and Gold; accept and complete one ordinary non-raid contract.
3. Confirm its state is Ready to Claim and the displayed Gold/XP matches the planned reward.
4. Claim once. Confirm XP rises by the displayed amount, Gold rises by the displayed amount, and inventory UI refreshes.
5. Reopen the board and attempt a second claim. Confirm no XP or Gold changes.
6. Zone, restart, and recheck the completed occurrence. Confirm no duplicate payment.
7. If safe, complete a contract while raid-active. Claim must say `Finish or leave the raid before claiming this contract`; Gold and XP must remain unchanged and the contract must remain Ready to Claim. Leave/finish the raid and claim it normally.
8. Obtain the reward diagnostic through the Suite control surface. Confirm `xpConfigValue=true`, `rewardSchema=1`, direct APIs available, and a truthful claim eligibility/result.

Expected recovery behavior:

- If preflight cannot prepare XP, Gold is not attempted.
- If a durable pre-invocation marker cannot be saved, no native reward is attempted.
- If a native call has an unknown outcome, that component is locked rather than retried.
