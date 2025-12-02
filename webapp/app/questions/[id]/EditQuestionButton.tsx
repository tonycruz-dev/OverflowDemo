"use client";

import { Button } from "@heroui/button";
import Link from "next/link";

type Props = {
  href: string;
};

export default function EditQuestionButton({ href }: Props) {
  return (
    <Button as={Link} href={href} size="sm" variant="faded" color="primary">
      Edit
    </Button>
  );
}
