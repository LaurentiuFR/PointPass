-- Read-only proof for a known completed match, including both players' history.
-- psql -X -v ON_ERROR_STOP=1 -v match_id=UUID -d pointpass -f AuditHistory.sql
BEGIN TRANSACTION READ ONLY;
SELECT id, state, blue_score, red_score, finished_utc
FROM matches WHERE id = :'match_id'::uuid;
SELECT slot, count(*) AS history_occurrences
FROM match_players mp JOIN matches m ON m.id = mp.match_id
WHERE m.id = :'match_id'::uuid AND m.state = 'completed'
GROUP BY slot ORDER BY slot;
SELECT (
    (SELECT count(*) FROM matches WHERE id = :'match_id'::uuid
        AND state = 'completed' AND blue_score = 2 AND red_score = 1) = 1
    AND (SELECT count(*) FROM match_players WHERE match_id = :'match_id'::uuid) = 2
    AND (SELECT count(DISTINCT slot) FROM match_players WHERE match_id = :'match_id'::uuid) = 2
) AS final_match_verified \gset
\if :final_match_verified
\echo PASS final 2-1 match occurs once with exactly two distinct participant histories
\else
\echo FAIL final match/history invariant
\quit 1
\endif
COMMIT;
