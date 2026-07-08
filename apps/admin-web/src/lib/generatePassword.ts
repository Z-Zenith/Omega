// AWA-10: generate a random temporary password client-side to hand to the admin,
// who relays it to the user out-of-band. Uses crypto.getRandomValues, not Math.random.
const CHARSET = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%'

export function generateTempPassword(length = 16): string {
  const bytes = new Uint32Array(length)
  crypto.getRandomValues(bytes)
  return Array.from(bytes, (n) => CHARSET[n % CHARSET.length]).join('')
}
