INSERT INTO "Transactions" 
    ("Id", "ExternalId", "Type", "Amount", "Currency", "AccountId", "OccurredAt","ReceivedAt", "ProcessedAt")
    VALUES 
    (gen_random_uuid(), 'seed-001', 'Card', 69.76, 'BGN', 'BG80BNBG96611176445678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-002', 'Transfer', 233.54, 'EUR', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-003', 'Transfer', 341.33, 'EUR', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-004', 'Card', 982.21, 'USD', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-005', 'Refund', 106.77, 'GBP', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-006', 'Transfer', 505.12, 'RON', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-007', 'Refund', 674.55, 'BGN', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-008', 'Refund', 11.33, 'EUR', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-009', 'Card', 771.14, 'USD', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now()),
    (gen_random_uuid(), 'seed-010', 'Transfer', 112.7, 'EUR', 'BG80BNBG96611176783678', now() - interval '10 minutes', now() - interval '5 minutes', now());    
    

