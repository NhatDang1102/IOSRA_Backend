ALTER TABLE subscription_plan ADD COLUMN initial_dias INT UNSIGNED NOT NULL DEFAULT 0 AFTER daily_dias;

UPDATE subscription_plan 
SET initial_dias = 500 
WHERE plan_code = 'premium_month';
