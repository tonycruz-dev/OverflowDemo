"use client";

import { Card, CardBody, CardFooter, CardHeader } from "@heroui/card";
import Link from "next/link";
import { Tag } from "@/lib/types";
import { Chip } from "@heroui/chip";
import { PencilSquareIcon, TrashIcon } from "@heroicons/react/24/outline";
import { User } from "next-auth";

export default function TagCard({
  tag,
  currentUser,
}: {
  tag: Tag;
  currentUser: User | null;
}) {
  const isAuthed = !!currentUser;
  return (
    <Card isHoverable className="relative">
      <CardHeader className="flex justify-between items-start gap-2">
        <Link href={`/questions?tag=${tag.slug}`} className="shrink-0">
          <Chip variant="bordered">{tag.slug}</Chip>
        </Link>
        {isAuthed && (
          <div className="flex gap-1 items-center">
            <Link
              href={`/tags/${tag.slug}/edit`}
              aria-label="Edit tag"
              className="p-1 rounded hover:bg-neutral-200"
            >
              <PencilSquareIcon className="h-4 w-4" />
            </Link>
            <Link
              href={`/tags/${tag.slug}/delete`}
              aria-label="Delete tag"
              className="p-1 rounded hover:bg-red-200"
            >
              <TrashIcon className="h-4 w-4 text-red-600" />
            </Link>
          </div>
        )}
      </CardHeader>
      <CardBody>
        <p className="line-clamp-3 text-sm leading-relaxed">
          {tag.description}
        </p>
      </CardBody>
      <CardFooter className="text-xs text-neutral-600">
        {tag.usageCount} {tag.usageCount === 1 ? "question" : "questions"}
      </CardFooter>
    </Card>
  );
}
