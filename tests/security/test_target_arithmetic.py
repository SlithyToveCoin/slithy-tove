"""Exact-integer boundary checks for the staged retarget calculation."""
import random
import unittest

MASK = (1 << 256) - 1


def bounded_scale(target, elapsed, interval, limit):
    quotient, remainder = divmod(target, interval)
    if quotient > limit // elapsed:
        return limit
    base = (quotient * elapsed) & MASK
    tail = ((remainder * elapsed) & MASK) // interval
    return limit if tail > limit - base else base + tail


class RetargetTests(unittest.TestCase):
    def test_full_width_target_stays_unchanged(self):
        target = 0xFFFF << 240
        self.assertEqual(bounded_scale(target, 3600, 3600, MASK), target)
        self.assertNotEqual(((target * 3600) & MASK) // 3600, target)

    def test_exact_result_and_saturation(self):
        rng = random.Random(20260909)
        for _ in range(10000):
            limit = rng.randrange(1, MASK + 1)
            target = rng.randrange(1, limit + 1)
            interval = rng.randrange(4, 1 << 61)
            elapsed = rng.randrange(max(1, interval // 4), interval * 4 + 1)
            self.assertEqual(bounded_scale(target, elapsed, interval, limit),
                             min(limit, target * elapsed // interval))


if __name__ == "__main__":
    unittest.main()
