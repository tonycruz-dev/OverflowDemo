"use client";
import * as React from "react";

import { HeroUIProvider, ToastProvider } from "@heroui/react";
import { useRouter } from "next/navigation";
import { ThemeProvider } from "next-themes";
import { useTagStore } from "@/lib/hooks/useTagStore";
import { getTags } from "@/lib/actions/tag-actions";

export default function Providers({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const setTags = useTagStore((state) => state.setTags);

  React.useEffect(() => {
    const loadTags = async () => {
      const { data: tags } = await getTags();
      if (tags) setTags(tags);
    };

    void loadTags();
  }, [setTags]);
  // 2. Wrap HeroUIProvider at the root of your app
  return (
    <HeroUIProvider navigate={router.push} className="h-full flex flex-col">
      <ToastProvider />
      <ThemeProvider attribute="class" defaultTheme="light">
        {children}
      </ThemeProvider>
    </HeroUIProvider>
  );
}