'use client';
import { Button } from "@heroui/button";
import { testAuth } from "@/lib/actions/auth-actions";
import { handleError, successToast } from "@/lib/util";

export default function AuthTestButton() {
    const onClick = async () => {
        // Implement authentication test logic here
        const {data, error} = await testAuth();
        if (error) handleError(error);
        if (data) successToast(data as string);
        console.log("Auth test button clicked");
    };

    return (
      <Button color="success" onPress={onClick} type="button">
        Test Auth
      </Button>
    );
}