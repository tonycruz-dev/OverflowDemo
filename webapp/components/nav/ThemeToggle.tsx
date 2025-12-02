"use client";

import { Button } from "@heroui/react";
import { useTheme } from "next-themes";
import { MoonIcon, SunIcon } from "@heroicons/react/24/solid";
import { useEffect, useState } from "react";

export default function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  // Defer rendering until after mount to prevent SSR/client mismatches
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const id = setTimeout(() => setMounted(true), 0);
    return () => clearTimeout(id);
  }, []);

  if (!mounted) return null;

  const isLight = resolvedTheme === "light";

  return (
    <Button
      color="primary"
      variant="light"
      isIconOnly
      aria-label="Toggle Theme"
      onPress={() => setTheme(isLight ? "dark" : "light")}
    >
      {isLight ? (
        <MoonIcon className="h-8" />
      ) : (
        <SunIcon className="h-8 text-yellow-300" />
      )}
    </Button>
  );
}
