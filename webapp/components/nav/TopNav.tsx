import { AcademicCapIcon } from "@heroicons/react/24/solid";
import Link from "next/link";
import ThemeToggle from "./ThemeToggle";
import SearchInput from "./SearchInput";
import LoginButton from "./LoginButton";
import { getCurrentUser } from "@/lib/actions/auth-actions";
import UserMenu from "./UserMenu";
import RegisterButton from "./RegisterButton";

export default async function TopNav() {
  const user = await getCurrentUser();
  return (
    <header className="p-2 w-full fixed top-0 z-50 border-b bg-white dark:bg-black">
      <div className="flex px-10 mx-auto">
        <div className="flex items-center gap-8">
          <Link href="/" className="flex items-center gap-4 max-h-16">
            <AcademicCapIcon className="size-10 text-secondary" />
            {/* Brand text fixed here */}
            <div className="flex flex-col leading-tight">
              <span className="text-xl font-bold tracking-wide uppercase">
                Overflow
              </span>
              <span className="text-[0.7rem] font-semibold tracking-[0.25em] uppercase text-neutral-500 dark:text-neutral-400">
                Powered by <span className="text-secondary">AI</span>
              </span>
            </div>
          </Link>
          <nav className="flex gap-3 my-2 text-medium text-neutral-500">
            <Link href="/">About</Link>
            <Link href="/">Product</Link>
            <Link href="/">Contact</Link>
          </nav>
        </div>
        <SearchInput />
        <div className="flex basis-1/4 shrink-0 justify-end gap-3 items-center">
          <ThemeToggle />
          {user ? (
            <UserMenu user={user} />
          ) : (
            <>
              <LoginButton />
              <RegisterButton />
            </>
          )}
        </div>
      </div>
    </header>
  );
}
