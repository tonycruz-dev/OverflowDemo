"use client";

import { MagnifyingGlassIcon } from "@heroicons/react/24/solid";
import { Input } from "@heroui/input";
import { Tab, Tabs } from "@heroui/tabs";
import { useRouter } from "next/navigation";
import Link from "next/link"; // added
import { Button } from "@heroui/button"; // added
import type { User } from "next-auth"; // added

export default function TagHeader({ currentUser }: { currentUser: User | null }) {
  const router = useRouter();
  //const { status } = useSession(); // existing
  // added
  const tabs = [
    { key: "popular", label: "Popular" },
    { key: "name", label: "Name" },
  ];
 
  const isAuthed = !!currentUser; 

  return (
    <div className="flex flex-col w-full gap-4 pb-4">
      <div className="flex flex-col items-start gap-3">
        <div className="text-3xl font-semibold">Tags </div>
        <p>
          A tag is a keyword or label that categorizes your question with other,
          similar questions. Using the right tags makes it easier for others to
          find and answer your question.
        </p>
      </div>
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <Input
          type="search"
          className="w-fit"
          required
          placeholder="Search"
          startContent={
            <MagnifyingGlassIcon className="h-6 text-neutral-500" />
          }
        />
        <div className="flex items-center gap-3">
          <Tabs
            onSelectionChange={(key) => router.push(`/tags?sort=${key}`)}
            defaultSelectedKey="name"
          >
            {tabs.map((item) => (
              <Tab key={item.key} title={item.label} />
            ))}
          </Tabs>
          {isAuthed && (
            <Button as={Link} href="/tags/new" color="secondary" variant="solid">
              Add Tag
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
