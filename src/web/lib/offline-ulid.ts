const alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

export function createUlid(timestamp = Date.now()): string {
  if (!Number.isSafeInteger(timestamp) || timestamp < 0 || timestamp > 281_474_976_710_655) {
    throw new RangeError("ULID timestamp is outside the supported 48-bit range.");
  }

  let remaining = timestamp;
  const time = Array.from({ length: 10 }, () => "0");
  for (let index = 9; index >= 0; index -= 1) {
    time[index] = alphabet[remaining % 32] ?? "0";
    remaining = Math.floor(remaining / 32);
  }

  const randomBytes = new Uint8Array(16);
  crypto.getRandomValues(randomBytes);
  const random = Array.from(randomBytes, (byte) => alphabet[byte & 31]).join("");
  return `${time.join("")}${random}`;
}
