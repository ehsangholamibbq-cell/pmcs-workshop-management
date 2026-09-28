import Image from "next/image";

/** The symbol is a transparent crop of the official Beton Baspar Qazvin mark. */
export function BrandMark() {
  return (
    <div className="brand-mark" aria-label="بتن بسپار قزوین، سامانه مدیریت پروژه">
      <Image src="/brand/bbq-official-symbol.png" alt="" width={52} height={27} priority unoptimized />
    </div>
  );
}
